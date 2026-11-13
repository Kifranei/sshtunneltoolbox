using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace SshTunnelToolbox;

/// <summary>
/// 用 XAML 片段创建带 {ThemeResource} 的元素：这样颜色会跟着元素所在的主题（浅色/深色）自动切换，
/// 在代码里直接取 Application.Resources 只能拿到应用级主题的画刷。
/// </summary>
internal static class X
{
	private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";

	public static Border Card(double padH = 14, double padV = 12)
	{
		return (Border)XamlReader.Load($"<Border {Ns} Background='{{ThemeResource CardBackgroundFillColorDefaultBrush}}' BorderBrush='{{ThemeResource CardStrokeColorDefaultBrush}}' BorderThickness='1' CornerRadius='8' Padding='{padH},{padV},{padH},{padV}'/>");
	}

	public static TextBlock Secondary(string text, bool wrap = false)
	{
		TextBlock tb = (TextBlock)XamlReader.Load($"<TextBlock {Ns} Foreground='{{ThemeResource TextFillColorSecondaryBrush}}'/>");
		tb.Text = text;
		tb.TextWrapping = wrap ? TextWrapping.WrapWholeWords : TextWrapping.NoWrap;
		tb.VerticalAlignment = VerticalAlignment.Center;
		return tb;
	}

	public static TextBlock Header(string text)
	{
		return new TextBlock { Text = text, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
	}

	public static TextBlock Label(string text)
	{
		return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
	}

	public static Button Btn(string text, RoutedEventHandler click, string glyph = null, bool accent = false)
	{
		object content = text;
		if (glyph != null)
		{
			StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
			sp.Children.Add(new TextBlock { Text = text });
			content = sp;
		}
		Button b = new Button { Content = content };
		if (accent)
		{
			b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
		}
		if (click != null)
		{
			b.Click += click;
		}
		return b;
	}

	public static void SetButtonText(Button b, string text)
	{
		if (b.Content is StackPanel sp && sp.Children.Count == 2 && sp.Children[1] is TextBlock tb)
		{
			tb.Text = text;
		}
		else
		{
			b.Content = text;
		}
	}

	public static Grid Row(params (GridLength width, UIElement el)[] cols)
	{
		Grid g = new Grid { ColumnSpacing = 8 };
		for (int i = 0; i < cols.Length; i++)
		{
			g.ColumnDefinitions.Add(new ColumnDefinition { Width = cols[i].width });
			if (cols[i].el != null)
			{
				Grid.SetColumn((FrameworkElement)cols[i].el, i);
				g.Children.Add(cols[i].el);
			}
		}
		return g;
	}

	public static GridLength Auto => GridLength.Auto;

	public static GridLength Star(double v = 1) => new GridLength(v, GridUnitType.Star);

	public static GridLength Px(double v) => new GridLength(v);
}
