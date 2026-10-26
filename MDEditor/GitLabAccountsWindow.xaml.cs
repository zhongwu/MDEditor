using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Non-modal, single-instance window (tracked on <see cref="MainWindow"/>) for connecting GitLab
/// accounts - gitlab.com or a self-hosted server - with a personal access token, and choosing the
/// active account.
/// </summary>
public partial class GitLabAccountsWindow : Window
{
    private readonly IGitLabAuthService _authService;
    private CancellationTokenSource? _operationCts;

    public GitLabAccountsWindow(IGitLabAuthService authService)
    {
        InitializeComponent();
        _authService = authService;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += (s, e) =>
        {
            // Pre-fill with the last server: adding a second account on the same server is the common case.
            ServerBox.Text = _authService.ActiveAccount?.ServerUrl ?? string.Empty;
            RefreshAccounts();
        };
        Closed += (s, e) => _operationCts?.Cancel();
        _authService.AccountsChanged += OnAccountsChanged;
        Closed += (s, e) => _authService.AccountsChanged -= OnAccountsChanged;
        Loc.LanguageChanged += OnAccountsChanged; // the account rows are built in code
        Closed += (s, e) => Loc.LanguageChanged -= OnAccountsChanged;
    }

    private void OnAccountsChanged(object? sender, EventArgs e) => RefreshAccounts();

    private void RefreshAccounts()
    {
        var selectedKey = (AccountsList.SelectedItem as ListBoxItem)?.Tag as string;
        var active = _authService.ActiveAccount;

        AccountsList.Items.Clear();
        foreach (var account in _authService.Accounts)
        {
            var isActive = account == active;
            var name = account.Name != null ? $" ({account.Name})" : string.Empty;
            var item = new ListBoxItem
            {
                Content = $"{account.DisplayName}{name}{(isActive ? Loc.Get("GL_ActiveSuffix") : string.Empty)}",
                Tag = account.Key,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                ToolTip = account.ServerVersion != null ? Loc.Format("GL_ServerVersionTip", account.ServerVersion) : null,
            };
            AccountsList.Items.Add(item);
            if (string.Equals(account.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
                AccountsList.SelectedItem = item;
        }

        NoAccountsText.Visibility = AccountsList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var busy = _operationCts != null;
        var selected = (AccountsList.SelectedItem as ListBoxItem)?.Tag as string;
        var selectedIsActive = selected != null
            && string.Equals(selected, _authService.ActiveAccount?.Key, StringComparison.OrdinalIgnoreCase);

        SetActiveButton.IsEnabled = !busy && selected != null && !selectedIsActive;
        RemoveButton.IsEnabled = !busy && selected != null;
        ConnectButton.IsEnabled = !busy;
    }

    private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void AccountsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SetActiveButton.IsEnabled)
            SetActive_Click(sender, e);
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as ListBoxItem)?.Tag is string key)
            _authService.SetActiveAccount(key);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as ListBoxItem)?.Tag is not string key)
            return;

        var account = _authService.Accounts.FirstOrDefault(a => a.Key == key);
        var label = account?.DisplayName ?? key;
        var result = MessageBox.Show(this, Loc.Format("GL_RemoveConfirm", label),
            Loc.Get("GL_RemoveTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        _authService.RemoveAccount(key);
        ShowStatus(Loc.Format("GL_Removed", label), isError: false);
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerBox.Text.Trim();
        var token = TokenBox.Password;
        if (server.Length == 0 || string.IsNullOrWhiteSpace(token))
        {
            ShowStatus(Loc.Get("GL_FillBoth"), isError: true);
            return;
        }

        _operationCts = new CancellationTokenSource();
        UpdateButtons();
        ShowStatus(Loc.Get("GL_Connecting"), isError: false);
        try
        {
            var account = await _authService.AddAccountAsync(server, token, SkipCertCheck.IsChecked == true, _operationCts.Token);
            TokenBox.Clear();
            ShowStatus(Loc.Format("GL_Connected", account.DisplayName), isError: false);
        }
        catch (OperationCanceledException)
        {
            // The window was closed mid-connect.
        }
        catch (Exception ex)
        {
            if (IsLoaded)
                ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            if (IsLoaded)
                UpdateButtons();
        }
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ConnectButton.IsEnabled)
        {
            e.Handled = true;
            Connect_Click(sender, e);
        }
    }

    private void CreateTokenLink_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerBox.Text.Trim();
        string root;
        try
        {
            root = server.Length == 0 ? "https://gitlab.com" : MDEditor.Core.Services.GitLabApiClient.NormalizeServerUrl(server);
        }
        catch (FormatException)
        {
            ShowStatus(Loc.Format("GL_Err_InvalidUrl", server), isError: true);
            return;
        }

        var url = $"{root}/-/profile/personal_access_tokens?name=MDEditor&scopes=api";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus(Loc.Format("GH_BrowserOpenFailedGoTo", ex.Message, url), isError: true);
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.IndianRed : (Brush)FindResource("WindowForegroundBrush");
        StatusText.Visibility = Visibility.Visible;
    }
}
