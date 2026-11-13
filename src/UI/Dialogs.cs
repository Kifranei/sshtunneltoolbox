using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SshTunnelToolbox;

/// <summary>ContentDialog 封装（同一时间只能显示一个，这里排队显示），主题跟随主窗口。</summary>
internal static class Dialogs
{
	public static FrameworkElement Root;

	/// <summary>弹窗前确保主窗口可见（例如从托盘触发时）。</summary>
	public static Action EnsureVisible;

	public static string DefaultTitle = "";

	private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

	public static async Task<ContentDialogResult> ShowAsync(ContentDialog d)
	{
		await Gate.WaitAsync();
		try
		{
			EnsureVisible?.Invoke();
			if (Root.XamlRoot == null)
			{
				TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
				RoutedEventHandler h = null;
				h = (s, e) =>
				{
					Root.Loaded -= h;
					tcs.TrySetResult(true);
				};
				Root.Loaded += h;
				if (Root.XamlRoot == null)
				{
					await tcs.Task;
				}
			}
			d.XamlRoot = Root.XamlRoot;
			d.RequestedTheme = Root.ActualTheme;
			d.Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"];
			return await d.ShowAsync();
		}
		finally
		{
			Gate.Release();
		}
	}

	public static TextBlock Text(string text)
	{
		return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
	}

	public static async Task Message(string text, string title = null)
	{
		ContentDialog d = new ContentDialog
		{
			Title = title ?? DefaultTitle,
			Content = new ScrollViewer { Content = Text(text), MaxHeight = 420 },
			CloseButtonText = "确定",
			DefaultButton = ContentDialogButton.Close
		};
		await ShowAsync(d);
	}

	public static async Task<bool> Confirm(string text, string title = null, string yes = "是", string no = "否", bool defaultNo = false)
	{
		ContentDialog d = new ContentDialog
		{
			Title = title ?? DefaultTitle,
			Content = new ScrollViewer { Content = Text(text), MaxHeight = 420 },
			PrimaryButtonText = yes,
			CloseButtonText = no,
			DefaultButton = defaultNo ? ContentDialogButton.Close : ContentDialogButton.Primary
		};
		return await ShowAsync(d) == ContentDialogResult.Primary;
	}

	/// <summary>三按钮询问：返回 Primary / Secondary / None（取消）。</summary>
	public static async Task<ContentDialogResult> Ask(string text, string title, string primary, string secondary, string cancel = "取消")
	{
		ContentDialog d = new ContentDialog
		{
			Title = title ?? DefaultTitle,
			Content = new ScrollViewer { Content = Text(text), MaxHeight = 420 },
			PrimaryButtonText = primary,
			SecondaryButtonText = secondary,
			CloseButtonText = cancel,
			DefaultButton = ContentDialogButton.Primary
		};
		return await ShowAsync(d);
	}

	/// <summary>单行输入框；validate 返回错误文字（null 表示通过）。取消返回 null。</summary>
	public static async Task<string> Input(string title, string prompt, string initial, Func<string, string> validate, double width = 420)
	{
		TextBox box = new TextBox { Text = initial ?? "", Width = width, HorizontalAlignment = HorizontalAlignment.Left };
		TextBlock err = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE8, 0x11, 0x23)) };
		StackPanel sp = new StackPanel { Spacing = 10 };
		sp.Children.Add(Text(prompt));
		sp.Children.Add(box);
		sp.Children.Add(err);
		ContentDialog d = new ContentDialog
		{
			Title = title,
			Content = sp,
			PrimaryButtonText = "确定",
			CloseButtonText = "取消",
			DefaultButton = ContentDialogButton.Primary
		};
		d.Opened += (s, e) =>
		{
			box.Focus(FocusState.Programmatic);
			box.SelectAll();
		};
		box.TextChanged += (s, e) => err.Visibility = Visibility.Collapsed;
		d.PrimaryButtonClick += (s, e) =>
		{
			string v = box.Text.Trim();
			string msg = validate?.Invoke(v);
			if (msg != null)
			{
				err.Text = msg;
				err.Visibility = Visibility.Visible;
				e.Cancel = true;
			}
		};
		if (await ShowAsync(d) != ContentDialogResult.Primary)
		{
			return null;
		}
		return box.Text.Trim();
	}
}
