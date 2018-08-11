﻿using System;
using System.Linq;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Models.Api;
using Bit.App.Resources;
using Xamarin.Forms;
using XLabs.Ioc;
using System.Threading.Tasks;
using Bit.App.Utilities;

namespace Bit.App.Pages
{
    public class RegisterPage : ExtendedContentPage
    {
        private ICryptoService _cryptoService;
        private IDeviceActionService _deviceActionService;
        private IAccountsApiRepository _accountsApiRepository;
        private IGoogleAnalyticsService _googleAnalyticsService;
        private HomePage _homePage;

        public RegisterPage(HomePage homePage)
            : base(updateActivity: false, requireAuth: false)
        {
            _homePage = homePage;
            _cryptoService = Resolver.Resolve<ICryptoService>();
            _deviceActionService = Resolver.Resolve<IDeviceActionService>();
            _accountsApiRepository = Resolver.Resolve<IAccountsApiRepository>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();

            Init();
        }

        public FormEntryCell EmailCell { get; set; }
        public FormEntryCell PasswordCell { get; set; }
        public FormEntryCell ConfirmPasswordCell { get; set; }
        public FormEntryCell PasswordHintCell { get; set; }
        public RedrawableStackLayout StackLayout { get; set; }
        public Label PasswordLabel { get; set; }
        public Label HintLabel { get; set; }

        private void Init()
        {
            MessagingCenter.Send(Application.Current, "ShowStatusBar", true);

            var padding = Helpers.OnPlatform(
                iOS: new Thickness(15, 20),
                Android: new Thickness(15, 8),
                Windows: new Thickness(10, 8));

            PasswordHintCell = new FormEntryCell(AppResources.MasterPasswordHint, useLabelAsPlaceholder: true,
                imageSource: "lightbulb.png", containerPadding: padding);
            ConfirmPasswordCell = new FormEntryCell(AppResources.RetypeMasterPassword, isPassword: true,
                nextElement: PasswordHintCell.Entry, useLabelAsPlaceholder: true, imageSource: "lock.png",
                containerPadding: padding);
            PasswordCell = new FormEntryCell(AppResources.MasterPassword, isPassword: true,
                nextElement: ConfirmPasswordCell.Entry, useLabelAsPlaceholder: true, imageSource: "lock.png",
                containerPadding: padding);
            EmailCell = new FormEntryCell(AppResources.EmailAddress, nextElement: PasswordCell.Entry,
                entryKeyboard: Keyboard.Email, useLabelAsPlaceholder: true, imageSource: "envelope.png",
                containerPadding: padding);

            PasswordHintCell.Entry.TargetReturnType = Enums.ReturnType.Done;

            var table = new FormTableView(this)
            {
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        EmailCell,
                        PasswordCell
                    }
                }
            };

            PasswordLabel = new Label
            {
                Text = AppResources.MasterPasswordDescription,
                LineBreakMode = LineBreakMode.WordWrap,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                Style = (Style)Application.Current.Resources["text-muted"],
                Margin = new Thickness(15, (this.IsLandscape() ? 5 : 0), 15, 25)
            };

            var table2 = new FormTableView(this)
            {
                NoHeader = true,
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        ConfirmPasswordCell,
                        PasswordHintCell
                    }
                }
            };

            HintLabel = new Label
            {
                Text = AppResources.MasterPasswordHintDescription,
                LineBreakMode = LineBreakMode.WordWrap,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                Style = (Style)Application.Current.Resources["text-muted"],
                Margin = new Thickness(15, (this.IsLandscape() ? 5 : 0), 15, 25)
            };

            StackLayout = new RedrawableStackLayout
            {
                Children = { table, PasswordLabel, table2, HintLabel },
                Spacing = 0
            };

            var scrollView = new ScrollView
            {
                Content = StackLayout
            };

            var loginToolbarItem = new ToolbarItem(AppResources.Submit, Helpers.ToolbarImage("ion_chevron_right.png"), async () =>
            {
                await Register();
            }, ToolbarItemOrder.Default, 0);

            if(Device.RuntimePlatform == Device.iOS)
            {
                table.RowHeight = table2.RowHeight = -1;
                table.EstimatedRowHeight = table2.EstimatedRowHeight = 70;
                ToolbarItems.Add(new DismissModalToolBarItem(this, AppResources.Cancel, () =>
                {
                    MessagingCenter.Send(Application.Current, "ShowStatusBar", false);
                }));
            }

            ToolbarItems.Add(loginToolbarItem);
            Title = AppResources.CreateAccount;
            Content = scrollView;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            MessagingCenter.Send(Application.Current, "ShowStatusBar", true);
            EmailCell.InitEvents();
            PasswordCell.InitEvents();
            PasswordHintCell.InitEvents();
            ConfirmPasswordCell.InitEvents();
            PasswordHintCell.Entry.Completed += Entry_Completed;
            EmailCell.Entry.FocusWithDelay();
        }
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            EmailCell.Dispose();
            PasswordCell.Dispose();
            PasswordHintCell.Dispose();
            ConfirmPasswordCell.Dispose();
            PasswordHintCell.Entry.Completed -= Entry_Completed;
        }

        private async void Entry_Completed(object sender, EventArgs e)
        {
            await Register();
        }

        private async Task Register()
        {
            if(string.IsNullOrWhiteSpace(EmailCell.Entry.Text))
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred,
                    string.Format(AppResources.ValidationFieldRequired, AppResources.EmailAddress), AppResources.Ok);
                return;
            }

            if(string.IsNullOrWhiteSpace(PasswordCell.Entry.Text))
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred,
                    string.Format(AppResources.ValidationFieldRequired, AppResources.MasterPassword), AppResources.Ok);
                return;
            }

            if(PasswordCell.Entry.Text.Length < 8)
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred, AppResources.MasterPasswordLengthValMessage,
                    AppResources.Ok);
                return;
            }

            if(ConfirmPasswordCell.Entry.Text != PasswordCell.Entry.Text)
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred, AppResources.MasterPasswordConfirmationValMessage,
                    AppResources.Ok);
                return;
            }

            var normalizedEmail = EmailCell.Entry.Text.ToLower();
            var key = _cryptoService.MakeKeyFromPassword(PasswordCell.Entry.Text, normalizedEmail);
            var encKey = _cryptoService.MakeEncKey(key);
            var request = new RegisterRequest
            {
                Email = normalizedEmail,
                MasterPasswordHash = _cryptoService.HashPasswordBase64(key, PasswordCell.Entry.Text),
                MasterPasswordHint = !string.IsNullOrWhiteSpace(PasswordHintCell.Entry.Text)
                    ? PasswordHintCell.Entry.Text : null,
                Key = encKey.EncryptedString
            };

            await _deviceActionService.ShowLoadingAsync(AppResources.CreatingAccount);
            var response = await _accountsApiRepository.PostRegisterAsync(request);
            await _deviceActionService.HideLoadingAsync();

            if(!response.Succeeded)
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred, response.Errors.FirstOrDefault()?.Message,
                    AppResources.Ok);
                return;
            }

            _googleAnalyticsService.TrackAppEvent("Registered");
            await _homePage.DismissRegisterAndLoginAsync(normalizedEmail);
        }

        private class FormTableView : ExtendedTableView
        {
            public FormTableView(RegisterPage page)
            {
                Intent = TableIntent.Settings;
                EnableScrolling = false;
                HasUnevenRows = true;
                EnableSelection = true;
                VerticalOptions = LayoutOptions.Start;
                NoFooter = true;
                WrappingStackLayout = () => page.StackLayout;
            }
        }
    }
}
