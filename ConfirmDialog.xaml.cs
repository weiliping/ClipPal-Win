using System.Windows;
using ClipPal.Services;

namespace ClipPal;

/// <summary>确认框（对标 macOS 版 NSAlert + showsSuppressionButton）。</summary>
public partial class ConfirmDialog : Window
{
    public bool DontAskAgain => DontAskBox.IsChecked == true;

    public ConfirmDialog(string title, string message, string okText, string cancelText, string dontAskText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        CancelButton.Content = cancelText;
        DontAskBox.Content = dontAskText;

        OkButton.Click += (_, _) => { DialogResult = true; Close(); };
        CancelButton.Click += (_, _) => { DialogResult = false; Close(); };
    }
}
