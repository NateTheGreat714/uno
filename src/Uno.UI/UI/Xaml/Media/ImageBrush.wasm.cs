using System;
using Windows.Foundation;
using Windows.UI.Xaml.Wasm;
using Uno.Disposables;
using System.Threading.Tasks;

namespace Windows.UI.Xaml.Media
{
	partial class ImageBrush
	{
		internal string ToCssPosition()
		{
			var x = AlignmentX switch
			{
				AlignmentX.Left => "left",
				AlignmentX.Center => "center",
				AlignmentX.Right => "right",
				_ => ""
			};

			var y = AlignmentY switch
			{
				AlignmentY.Top=> "top",
				AlignmentY.Center => "center",
				AlignmentY.Bottom => "bottom",
				_ => ""
			};

			return $"{x} {y}";
		}
		internal string ToCssBackgroundSize()
		{
			return Stretch switch
			{
				Stretch.Fill => "100% 100%",
				Stretch.None => "auto",
				Stretch.Uniform => "auto", // patch for now
				Stretch.UniformToFill => "auto", // patch for now
				_ => "auto"
			};
		}

		internal async void SetStretchToNone(UIElement defElement, string preserveAspectRatio, ImageData data, double width, double height)
		{
			var command = "Uno.UI.WindowManager.current.getNaturalImageSize(\"" + data.Value + "\");";
			var naturalSize = await Uno.Foundation.WebAssemblyRuntime.InvokeAsync(command);
			var sizes = naturalSize.Split(';');
			Console.WriteLine($"Got Size: {naturalSize}");
			var imgElement = defElement.FindFirstChild();
			if (imgElement != null)
			{
				imgElement.SetAttribute(
					("width", sizes[0]),
					("height", sizes[1])
				);
			}

			var viewBoxAlignX = AlignmentX switch
			{
				AlignmentX.Left => 0,
				AlignmentX.Center => (int.Parse(sizes[0]) - width) / 2,
				AlignmentX.Right => (int.Parse(sizes[0]) - width),
				_ => 0
			};
			var viewBoxAlignY = AlignmentY switch
			{
				AlignmentY.Top => 0,
				AlignmentY.Center => (int.Parse(sizes[1]) - height) / 2,
				AlignmentY.Bottom => (int.Parse(sizes[1]) - height),
				_ => 0
			};
			//defElement.RemoveAttribute("patternContentUnits");
			defElement.SetAttribute(("viewBox", $"{viewBoxAlignX} {viewBoxAlignY} {width} {height}"));
			//defElement.SetHtmlContent($"<image width=\"{sizes[0]}\" height=\"{sizes[1]}\" preserveAspectRatio=\"{preserveAspectRatio}\" xlink:href=\"{data.Value}\" />");
		}

		internal (UIElement defElement, IDisposable subscription) ToSvgElement(double width, double height)
		{
			var pattern = new SvgElement("pattern");

			var alignX = AlignmentX switch
			{
				AlignmentX.Left => "xMin",
				AlignmentX.Center => "xMid",
				AlignmentX.Right => "xMax",
				_ => string.Empty
			};
			var alignY = AlignmentY switch
			{
				AlignmentY.Top => "YMin",
				AlignmentY.Center => "YMid",
				AlignmentY.Bottom => "YMax",
				_ => string.Empty
			};

			var preserveAspectRatio = Stretch switch
				{
					Stretch.None => $"{alignX}{alignY}",
					Stretch.Fill => "none",
					Stretch.Uniform => $"{alignX}{alignY} meet",
					Stretch.UniformToFill => $"{alignX}{alignY} slice",
					_ => string.Empty
				};



			// Using this solution to set the viewBox/Size
			// https://stackoverflow.com/a/13915777/1176099

			pattern.SetAttribute(
				("x", "0"),
				("y", "0"),
				("width", "100%"),
				("height", "100%")
			);

			if (Stretch == Stretch.UniformToFill)
			{
				pattern.SetAttribute(
					("preserveAspectRatio", $"{preserveAspectRatio}")
				);
			}


			var subscriptionDisposable = new SerialDisposable();

			var imageSourceChangedSubscription =
				this.RegisterDisposablePropertyChangedCallback(ImageSourceProperty, OnImageSourceChanged);

			void OnImageSourceChanged(DependencyObject dependencyobject, DependencyPropertyChangedEventArgs args)
			{
				var newImageSource = (args.NewValue as ImageSource);
				subscriptionDisposable.Disposable = newImageSource?.Subscribe(OnSourceOpened);
			}

			subscriptionDisposable.Disposable = ImageSource?.Subscribe(OnSourceOpened);

			void OnSourceOpened(ImageData data)
			{
				switch (data.Kind)
				{
					case ImageDataKind.Empty:
						pattern.ClearChildren();
						break;
					case ImageDataKind.DataUri:
					case ImageDataKind.Url:
						
						var image = new SvgElement("image");
						image.SetAttribute(
							("width", "100%"),
							("height", "100%"),
							("preserveAspectRatio", preserveAspectRatio),
							("href", data.Value)
								
						);
						pattern.AddChild(image);
						//pattern.SetHtmlContent($"<image width=\"100%\" height=\"100%\" preserveAspectRatio=\"{preserveAspectRatio}\" xlink:href=\"{data.Value}\" />");
						
						if (Stretch == Stretch.None)
						{
							SetStretchToNone(pattern, preserveAspectRatio, data, width, height);
						}

						break;
				}
			}

			var subscriptions = new CompositeDisposable(imageSourceChangedSubscription, subscriptionDisposable);

			return (pattern, subscriptions);
		}
	}
}
