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
        Loc.LanguageChanged += OnAccountsChanged; // the account rows are built in code
        Closed += (s, e) => Loc.LanguageChanged -= OnAccountsChanged;
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
            var method = account.AuthMethod == GitHubAuthMethod.OAuthDevice ? Loc.Get("GH_MethodBrowser") : Loc.Get("GH_MethodPat");
            var name = account.Name != null ? $" ({account.Name})" : string.Empty;
            var item = new ListBoxItem
            {
                Content = $"{account.Login}{name} - {method}{(isActive ? Loc.Get("GH_ActiveSuffix") : string.Empty)}",
                Tag = account.Login,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                ToolTip = account.Scopes.Count > 0 ? Loc.Format("GH_Scopes", string.Join(", ", account.Scopes)) : Loc.Get("GH_ScopesUnknown"),
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
            ? Loc.Get("GH_NeedClientIdTip")
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
            Loc.Format("GH_RemoveConfirm", login),
            Loc.Get("GH_RemoveTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        try
        {
            _authService.RemoveAccount(login);
            ShowStatus(Loc.Format("GH_Removed", login), isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus(Loc.Format("GH_RemoveFailed", ex.Message), isError: true);
        }
    }

    private async void DeviceFlow_Click(object sender, RoutedEventArgs e)
    {
        CommitClientId();
        if (string.IsNullOrWhiteSpace(_authService.OAuthClientId))
        {
            ShowStatus(Loc.Get("GH_NeedClientId"), isError: true);
            return;
        }

        await RunOperationAsync(async ct =>
        {
            _deviceCode = await _authService.StartDeviceFlowAsync(ct);
            UserCodeText.Text = _deviceCode.UserCode;
            DeviceFlowStatusText.Text = Loc.Format("GH_WaitingExpires", _deviceCode.ExpiresAt.LocalDateTime.ToString("t", Loc.Culture));
            DeviceFlowPanel.Visibility = Visibility.Visible;

            CopyCodeAndOpenBrowser();

            var account = await _authService.CompleteDeviceFlowAsync(_deviceCode, ct);
            Activate();
            ShowStatus(Loc.Format("GH_SignedInAs", account.Login), isError: false);
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
            ShowStatus(Loc.Get("GH_PasteTokenFirst"), isError: true);
            return;
        }

        await RunOperationAsync(async ct =>
        {
            ShowStatus(Loc.Get("GH_CheckingToken"), isError: false);
            var account = await _authService.AddAccountWithTokenAsync(token, ct);
            TokenBox.Clear();
            ShowStatus(Loc.Format("GH_ConnectedAccount", account.Login), isError: false);
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
                ShowStatus(Loc.Get("GH_SignInCancelled"), isError: false);
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
            ShowStatus(Loc.Format("GH_BrowserOpenFailedGoTo", ex.Message, url), isError: true);
        }
    }
}
