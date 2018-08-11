﻿using System;
using System.Linq;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Models.Page;
using Bit.App.Resources;
using Xamarin.Forms;
using XLabs.Ioc;
using Bit.App.Utilities;
using Plugin.Connectivity.Abstractions;
using Bit.App.Models;
using System.Threading.Tasks;

namespace Bit.App.Pages
{
    public class VaultAttachmentsPage : ExtendedContentPage
    {
        private readonly ICipherService _cipherService;
        private readonly IConnectivity _connectivity;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;
        private readonly ITokenService _tokenService;
        private readonly ICryptoService _cryptoService;
        private readonly string _cipherId;
        private Cipher _cipher;
        private byte[] _fileBytes;
        private DateTime? _lastAction;
        private bool _canUseAttachments = true;

        public VaultAttachmentsPage(string cipherId)
            : base(true)
        {
            _cipherId = cipherId;
            _cipherService = Resolver.Resolve<ICipherService>();
            _connectivity = Resolver.Resolve<IConnectivity>();
            _deviceActionService = Resolver.Resolve<IDeviceActionService>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();
            _tokenService = Resolver.Resolve<ITokenService>();
            _cryptoService = Resolver.Resolve<ICryptoService>();

            Init();
        }

        public ExtendedObservableCollection<VaultAttachmentsPageModel.Attachment> PresentationAttchments { get; private set; }
            = new ExtendedObservableCollection<VaultAttachmentsPageModel.Attachment>();
        public ExtendedListView ListView { get; set; }
        public RedrawableStackLayout NoDataStackLayout { get; set; }
        public StackLayout AddNewStackLayout { get; set; }
        public Label FileLabel { get; set; }
        public ExtendedTableView NewTable { get; set; }
        public Label NoDataLabel { get; set; }
        public ToolbarItem SaveToolbarItem { get; set; }

        private void Init()
        {
            _canUseAttachments = _cryptoService.EncKey != null;

            SubscribeFileResult(true);
            var selectButton = new ExtendedButton
            {
                Text = AppResources.ChooseFile,
                Command = new Command(async () => await _deviceActionService.SelectFileAsync()),
                Style = (Style)Application.Current.Resources["btn-primaryAccent"],
                FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Button))
            };

            FileLabel = new Label
            {
                Text = AppResources.NoFileChosen,
                Style = (Style)Application.Current.Resources["text-muted"],
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                HorizontalTextAlignment = TextAlignment.Center
            };

            AddNewStackLayout = new StackLayout
            {
                Children = { selectButton, FileLabel },
                Orientation = StackOrientation.Vertical,
                Padding = new Thickness(20, Helpers.OnPlatform(iOS: 10, Android: 20), 20, 20),
                VerticalOptions = LayoutOptions.Start
            };

            NewTable = new ExtendedTableView
            {
                Intent = TableIntent.Settings,
                HasUnevenRows = true,
                NoFooter = true,
                EnableScrolling = false,
                EnableSelection = false,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, Helpers.OnPlatform(iOS: 10, Android: 30), 0, 0),
                WrappingStackLayout = () => NoDataStackLayout,
                Root = new TableRoot
                {
                    new TableSection(AppResources.AddNewAttachment)
                    {
                        new ExtendedViewCell
                        {
                            View = AddNewStackLayout,
                            BackgroundColor = Color.White
                        }
                    }
                }
            };

            ListView = new ExtendedListView(ListViewCachingStrategy.RecycleElement)
            {
                ItemsSource = PresentationAttchments,
                HasUnevenRows = true,
                ItemTemplate = new DataTemplate(() => new VaultAttachmentsViewCell()),
                VerticalOptions = LayoutOptions.FillAndExpand
            };

            NoDataLabel = new Label
            {
                Text = AppResources.NoAttachments,
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                Style = (Style)Application.Current.Resources["text-muted"]
            };

            NoDataStackLayout = new RedrawableStackLayout
            {
                VerticalOptions = LayoutOptions.Start,
                Spacing = 0,
                Margin = new Thickness(0, 40, 0, 0)
            };

            SaveToolbarItem = new ToolbarItem(AppResources.Save, Helpers.ToolbarImage("envelope.png"), async () =>
            {
                if(_lastAction.LastActionWasRecent() || _cipher == null)
                {
                    return;
                }
                _lastAction = DateTime.UtcNow;


                if(!_canUseAttachments)
                {
                    await ShowUpdateKeyAsync();
                    return;
                }

                if(!_connectivity.IsConnected)
                {
                    AlertNoConnection();
                    return;
                }

                if(_fileBytes == null)
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired,
                        AppResources.File), AppResources.Ok);
                    return;
                }

                await _deviceActionService.ShowLoadingAsync(AppResources.Saving);
                var saveTask = await _cipherService.EncryptAndSaveAttachmentAsync(_cipher, _fileBytes, FileLabel.Text);
                await _deviceActionService.HideLoadingAsync();

                if(saveTask.Succeeded)
                {
                    _fileBytes = null;
                    FileLabel.Text = AppResources.NoFileChosen;
                    _deviceActionService.Toast(AppResources.AttachementAdded);
                    _googleAnalyticsService.TrackAppEvent("AddedAttachment");
                    await LoadAttachmentsAsync();
                }
                else if(saveTask.Errors.Count() > 0)
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, saveTask.Errors.First().Message, AppResources.Ok);
                }
                else
                {
                    await DisplayAlert(null, AppResources.AnErrorHasOccurred, AppResources.Ok);
                }
            }, ToolbarItemOrder.Default, 0);

            Title = AppResources.Attachments;
            Content = ListView;

            if(Device.RuntimePlatform == Device.iOS)
            {
                ListView.RowHeight = -1;
                NewTable.RowHeight = -1;
                NewTable.EstimatedRowHeight = 44;
                NewTable.HeightRequest = 180;
                ListView.BackgroundColor = Color.Transparent;
                ToolbarItems.Add(new DismissModalToolBarItem(this, AppResources.Close));
            }
            else if(Device.RuntimePlatform == Device.Android)
            {
                ListView.BottomPadding = 50;
            }
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();
            ListView.ItemSelected += AttachmentSelected;
            await LoadAttachmentsAsync();

            // Prevent from adding multiple save buttons
            if(Device.RuntimePlatform == Device.iOS && ToolbarItems.Count > 1)
            {
                ToolbarItems.RemoveAt(1);
            }
            else if(Device.RuntimePlatform != Device.iOS && ToolbarItems.Count > 0)
            {
                ToolbarItems.RemoveAt(0);
            }

            if(_cipher != null && (_tokenService.TokenPremium || _cipher.OrganizationId != null))
            {
                ToolbarItems.Add(SaveToolbarItem);
                ListView.Footer = NewTable;

                if(!_canUseAttachments)
                {
                    await ShowUpdateKeyAsync();
                }
            }
            else
            {
                await DisplayAlert(null, AppResources.PremiumRequired, AppResources.Ok);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ListView.ItemSelected -= AttachmentSelected;
        }

        private async Task LoadAttachmentsAsync()
        {
            _cipher = await _cipherService.GetByIdAsync(_cipherId);
            if(_cipher == null)
            {
                await Navigation.PopForDeviceAsync();
                return;
            }

            var attachmentsToAdd = _cipher.Attachments
                .Select(a => new VaultAttachmentsPageModel.Attachment(a, _cipher.OrganizationId))
                .OrderBy(s => s.Name);
            PresentationAttchments.ResetWithRange(attachmentsToAdd);
            AdjustContent();
        }

        private void AdjustContent()
        {
            if(PresentationAttchments.Count == 0)
            {
                NoDataStackLayout.Children.Clear();
                NoDataStackLayout.Children.Add(NoDataLabel);
                NoDataStackLayout.Children.Add(NewTable);
                Content = NoDataStackLayout;
            }
            else
            {
                Content = ListView;
            }
        }

        private async void AttachmentSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var attachment = e.SelectedItem as VaultAttachmentsPageModel.Attachment;
            if(attachment == null)
            {
                return;
            }

            ((ListView)sender).SelectedItem = null;

            var confirmed = await DisplayAlert(null, AppResources.DoYouReallyWantToDelete, AppResources.Yes,
                AppResources.No);
            if(!confirmed)
            {
                return;
            }

            await _deviceActionService.ShowLoadingAsync(AppResources.Deleting);
            var saveTask = await _cipherService.DeleteAttachmentAsync(_cipher, attachment.Id);
            await _deviceActionService.HideLoadingAsync();

            if(saveTask.Succeeded)
            {
                _deviceActionService.Toast(AppResources.AttachmentDeleted);
                _googleAnalyticsService.TrackAppEvent("DeletedAttachment");
                await LoadAttachmentsAsync();
            }
            else if(saveTask.Errors.Count() > 0)
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred, saveTask.Errors.First().Message, AppResources.Ok);
            }
            else
            {
                await DisplayAlert(null, AppResources.AnErrorHasOccurred, AppResources.Ok);
            }
        }

        private void AlertNoConnection()
        {
            DisplayAlert(AppResources.InternetConnectionRequiredTitle, AppResources.InternetConnectionRequiredMessage,
                AppResources.Ok);
        }

        private void SubscribeFileResult(bool subscribe)
        {
            MessagingCenter.Unsubscribe<Application, Tuple<byte[], string>>(Application.Current, "SelectFileResult");
            if(!subscribe)
            {
                return;
            }

            MessagingCenter.Subscribe<Application, Tuple<byte[], string>>(
                Application.Current, "SelectFileResult", (sender, result) =>
             {
                 FileLabel.Text = result.Item2;
                 _fileBytes = result.Item1;
                 SubscribeFileResult(true);
             });
        }

        private async Task ShowUpdateKeyAsync()
        {
            var confirmed = await DisplayAlert(AppResources.FeatureUnavailable, AppResources.UpdateKey,
                AppResources.LearnMore, AppResources.Cancel);
            if(confirmed)
            {
                Device.OpenUri(new Uri("https://help.bitwarden.com/article/update-encryption-key/"));
            }
        }
    }
}
