using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Non-modal, single-instance window (tracked on <see cref="MainWindow"/>) for connecting GitHub
/// accounts via OAuth device flow or Personal Access Token, and choosing the active account.
/// </summary>
public partial class GitHubAccountsWindow : Window
{
    private const string NewTokenUrl = "https://github.com/settings/tokens/new?scopes=repo,gist,read:org&description=MDEditor";

    private readonly IGitHubAuthService _authService;
    private CancellationTokenSource? _operationCts;
    private GitHubDeviceCode? _deviceCode;

    public GitHubAccountsWindow(IGitHubAuthService authService)
    {
        InitializeComponent();
        _authService = authService;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += (s, e) =>
        {
            ClientIdBox.Text = _authService.OAuthClientId;
            RefreshAccounts();
        };
        Closed += (s, e) => _operationCts?.Cancel();
        _authService.AccountsChanged += OnAccountsChanged;
        Closed += (s, e) => _authService.AccountsChanged -= OnAccountsChanged;
    }

    private void OnAccountsChanged(object? sender, EventArgs e) => RefreshAccounts();

    private void RefreshAccounts()
    {
        var selectedLogin = (AccountsList.SelectedItem as ListBoxItem)?.Tag as string;
        var active = _authService.ActiveAccount;

        AccountsList.Items.Clear();
        foreach (var account in _authService.Accounts)
        {
            bool isActive = account == active;
            var method = account.AuthMethod == GitHubAuthMethod.OAuthDevice ? "Browser sign-in" : "Personal access token";
            var name = account.Name != null ? $" ({account.Name})" : string.Empty;
            var item = new ListBoxItem
            {
                Content = $"{account.Login}{name} - {method}{(isActive ? "  [Active]" : string.Empty)}",
                Tag = account.Login,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                ToolTip = account.Scopes.Count > 0 ? "Scopes: " + string.Join(", ", account.Scopes) : "Scopes: not reported (fine-grained token)",
            };
            AccountsList.Items.Add(item);
            if (string.Equals(account.Login, selectedLogin, StringComparison.OrdinalIgnoreCase))
                AccountsList.SelectedItem = item;
        }

        NoAccountsText.Visibility = AccountsList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool busy = _operationCts != null;
        var selected = (AccountsList.SelectedItem as ListBoxItem)?.Tag as string;
        bool selectedIsActive = selected != null
            && string.Equals(selected, _authService.ActiveAccount?.Login, StringComparison.OrdinalIgnoreCase);

        SetActiveButton.IsEnabled = !busy && selected != null && !selectedIsActive;
        RemoveButton.IsEnabled = !busy && selected != null;
        AddTokenButton.IsEnabled = !busy;
        DeviceFlowButton.IsEnabled = !busy;
        DeviceFlowButton.ToolTip = string.IsNullOrWhiteSpace(_authService.OAuthClientId)
            ? "Set an OAuth App Client ID under Advanced to enable browser sign-in."
            : null;
    }

    private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void AccountsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SetActiveButton.IsEnabled)
            SetActive_Click(sender, e);
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as ListBoxItem)?.Tag is string login)
            _authService.SetActiveAccount(login);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as ListBoxItem)?.Tag is not string login)
            return;

        var result = MessageBox.Show(this,
            $"Remove \"{login}\" from MDEditor?\n\nThe stored token is deleted from this computer. To revoke it completely, also remove it on github.com (Settings > Applications, or Developer settings > Personal access tokens).",
            "Remove Account", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        try
        {
            _authService.RemoveAccount(login);
            ShowStatus($"Removed {login}.", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Couldn't remove the account: {ex.Message}", isError: true);
        }
    }

    private async void DeviceFlow_Click(object sender, RoutedEventArgs e)
    {
        CommitClientId();
        if (string.IsNullOrWhiteSpace(_authService.OAuthClientId))
        {
            ShowStatus("Browser sign-in needs an OAuth App Client ID - set one under Advanced below, or use a Personal Access Token.", isError: true);
            return;
        }

        await RunOperationAsync(async ct =>
        {
            _deviceCode = await _authService.StartDeviceFlowAsync(ct);
            UserCodeText.Text = _deviceCode.UserCode;
            DeviceFlowStatusText.Text = $"Waiting for you to approve on GitHub... (code expires at {_deviceCode.ExpiresAt.LocalDateTime:t})";
            DeviceFlowPanel.Visibility = Visibility.Visible;

            CopyCodeAndOpenBrowser();

            var account = await _authService.CompleteDeviceFlowAsync(_deviceCode, ct);
            Activate();
            ShowStatus($"Signed in as {account.Login}.", isError: false);
        });

        _deviceCode = null;
        DeviceFlowPanel.Visibility = Visibility.Collapsed;
    }

    private void CopyAndOpen_Click(object sender, RoutedEventArgs e) => CopyCodeAndOpenBrowser();

    private void CopyCodeAndOpenBrowser()
    {
        if (_deviceCode == null)
            return;

        try
        {
            Clipboard.SetText(_deviceCode.UserCode);
        }
        catch
        {
            // Clipboard can be locked by another process; the code is still shown in the window.
        }

        OpenUrl(_deviceCode.VerificationUri);
    }

    private void CancelDeviceFlow_Click(object sender, RoutedEventArgs e) => _operationCts?.Cancel();

    private async void AddToken_Click(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Password;
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowStatus("Paste a Personal Access Token first.", isError: true);
            return;
        }

        await RunOperationAsync(async ct =>
        {
            ShowStatus("Checking token with GitHub...", isError: false);
            var account = await _authService.AddAccountWithTokenAsync(token, ct);
            TokenBox.Clear();
            ShowStatus($"Connected {account.Login}.", isError: false);
        });
    }

    private void TokenBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && AddTokenButton.IsEnabled)
        {
            e.Handled = true;
            AddToken_Click(sender, e);
        }
    }

    /// <summary>
    /// Runs one sign-in operation at a time with a shared cancellation source, disabling the
    /// add/remove buttons while it runs and reporting failures in the status line.
    /// </summary>
    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        _operationCts = new CancellationTokenSource();
        UpdateButtons();
        try
        {
            await operation(_operationCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (IsLoaded)
                ShowStatus("Sign-in cancelled.", isError: false);
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

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.IndianRed : (Brush)FindResource("WindowForegroundBrush");
        StatusText.Visibility = Visibility.Visible;
    }

    private void ClientIdBox_LostFocus(object sender, RoutedEventArgs e) => CommitClientId();

    private void CommitClientId()
    {
        if (ClientIdBox.Text.Trim() != _authService.OAuthClientId)
        {
            _authService.OAuthClientId = ClientIdBox.Text;
            UpdateButtons();
        }
    }

    private void CreateTokenLink_Click(object sender, RoutedEventArgs e) => OpenUrl(NewTokenUrl);

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus($"Couldn't open the browser ({ex.Message}). Go to {url}", isError: true);
        }
    }
}
