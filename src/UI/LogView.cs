using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SshTunnelToolbox;

/// <summary>彩色日志：每行「时间 + 内容」，颜色按级别并随浅色/深色主题切换。</summary>
internal sealed class LogView
{
	private const int MaxLines = 2000;

	private readonly RichTextBlock _rtb;

	private readonly ScrollViewer _sv;

	private readonly string _timeFormat;

	private readonly List<(Paragraph p, Run time, Run msg, string level)> _lines = new List<(Paragraph, Run, Run, string)>();

	private bool _scrollPending;

	public FrameworkElement Element => _sv;

	public LogView(string fontFamily, double fontSize, string timeFormat)
	{
		_timeFormat = timeFormat;
		_rtb = new RichTextBlock
		{
			IsTextSelectionEnabled = true,
			TextWrapping = TextWrapping.Wrap,
			FontFamily = new FontFamily(fontFamily),
			FontSize = fontSize,
			Margin = new Thickness(10, 8, 14, 8)
		};
		_sv = new ScrollViewer
		{
			Content = _rtb,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		_sv.ActualThemeChanged += (s, e) => Recolor();
	}

	public void Clear()
	{
		_rtb.Blocks.Clear();
		_lines.Clear();
	}

	public void Append(string msg, string level)
	{
		bool dark = _sv.ActualTheme == ElementTheme.Dark;
		string[] parts = (msg ?? "").Replace("\r\n", "\n").Split('\n');
		foreach (string line in parts)
		{
			Run t = new Run { Text = "[" + DateTime.Now.ToString(_timeFormat) + "] ", Foreground = new SolidColorBrush(TimeColor(dark)) };
			Run m = new Run { Text = line, Foreground = new SolidColorBrush(ColorOf(level, dark)) };
			if (level == "Error")
			{
				m.FontWeight = FontWeights.SemiBold;
			}
			Paragraph p = new Paragraph();
			p.Inlines.Add(t);
			p.Inlines.Add(m);
			_rtb.Blocks.Add(p);
			_lines.Add((p, t, m, level));
		}
		while (_lines.Count > MaxLines)
		{
			_rtb.Blocks.Remove(_lines[0].p);
			_lines.RemoveAt(0);
		}
		ScrollToEnd();
	}

	private void ScrollToEnd()
	{
		if (_scrollPending)
		{
			return;
		}
		_scrollPending = true;
		_sv.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
		{
			_scrollPending = false;
			_sv.UpdateLayout();
			_sv.ChangeView(null, _sv.ScrollableHeight, null, true);
		});
	}

	private void Recolor()
	{
		bool dark = _sv.ActualTheme == ElementTheme.Dark;
		foreach ((Paragraph p, Run time, Run msg, string level) in _lines)
		{
			time.Foreground = new SolidColorBrush(TimeColor(dark));
			msg.Foreground = new SolidColorBrush(ColorOf(level, dark));
		}
	}

	private static Color TimeColor(bool dark)
	{
		return dark ? Color.FromArgb(255, 0x8C, 0x8C, 0x8C) : Color.FromArgb(255, 0x8C, 0x8C, 0x8C);
	}

	public static Color ColorOf(string level, bool dark)
	{
		return level switch
		{
			"Ok" => dark ? Color.FromArgb(255, 0x6C, 0xCB, 0x5F) : Color.FromArgb(255, 0x0F, 0x7B, 0x0F),
			"Warn" => dark ? Color.FromArgb(255, 0xF7, 0xB9, 0x55) : Color.FromArgb(255, 0x9D, 0x5D, 0x00),
			"Error" => dark ? Color.FromArgb(255, 0xFF, 0x99, 0xA4) : Color.FromArgb(255, 0xC4, 0x2B, 0x1C),
			"Detail" => dark ? Color.FromArgb(255, 0xA0, 0xA0, 0xA0) : Color.FromArgb(255, 0x6E, 0x6E, 0x6E),
			"Plink" => dark ? Color.FromArgb(255, 0xA8, 0xB4, 0xFF) : Color.FromArgb(255, 0x3C, 0x3C, 0x8C),
			_ => dark ? Color.FromArgb(255, 0xF0, 0xF0, 0xF0) : Color.FromArgb(255, 0x1A, 0x1A, 0x1A)
		};
	}
}
