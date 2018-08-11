﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Models.Page;
using Bit.App.Resources;
using Xamarin.Forms;
using XLabs.Ioc;
using Bit.App.Utilities;
using System.Threading;
using Bit.App.Models;
using System.Collections.Generic;
using Bit.App.Enums;
using static Bit.App.Models.Page.VaultListPageModel;
using Plugin.Connectivity.Abstractions;

namespace Bit.App.Pages
{
    public class VaultAutofillListCiphersPage : ExtendedContentPage
    {
        private readonly ICipherService _cipherService;
        private readonly IDeviceInfoService _deviceInfoService;
        private readonly ISettingsService _settingsService;
        private readonly IAppSettingsService _appSettingsService;
        public readonly IConnectivity _connectivity;
        private CancellationTokenSource _filterResultsCancellationTokenSource;
        private readonly string _name;
        private readonly AppOptions _appOptions;

        public VaultAutofillListCiphersPage(AppOptions appOptions)
            : base(true)
        {
            _appOptions = appOptions;
            Uri = appOptions.Uri;
            if(Uri.StartsWith(Constants.AndroidAppProtocol))
            {
                _name = Uri.Substring(Constants.AndroidAppProtocol.Length);
            }
            else if(!System.Uri.TryCreate(Uri, UriKind.Absolute, out Uri uri) ||
                !DomainName.TryParseBaseDomain(uri.Host, out _name))
            {
                _name = "--";
            }

            _cipherService = Resolver.Resolve<ICipherService>();
            _deviceInfoService = Resolver.Resolve<IDeviceInfoService>();
            DeviceActionService = Resolver.Resolve<IDeviceActionService>();
            _settingsService = Resolver.Resolve<ISettingsService>();
            _appSettingsService = Resolver.Resolve<IAppSettingsService>();
            GoogleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();
            _connectivity = Resolver.Resolve<IConnectivity>();

            Init();
        }

        public ContentView ContentView { get; set; }
        public Fab Fab { get; set; }
        public ExtendedObservableCollection<Section<AutofillCipher>> PresentationCiphersGroup { get; private set; }
            = new ExtendedObservableCollection<Section<AutofillCipher>>();
        public StackLayout NoDataStackLayout { get; set; }
        public ExtendedListView ListView { get; set; }
        public ActivityIndicator LoadingIndicator { get; set; }
        private SearchToolBarItem SearchItem { get; set; }
        private IGoogleAnalyticsService GoogleAnalyticsService { get; set; }
        private IDeviceActionService DeviceActionService { get; set; }
        private string Uri { get; set; }

        private void Init()
        {
            var noDataLabel = new Label
            {
                Text = string.Format(AppResources.NoItemsForUri, _name ?? "--"),
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                Style = (Style)Application.Current.Resources["text-muted"]
            };

            var addCipherButton = new ExtendedButton
            {
                Text = AppResources.AddAnItem,
                Command = new Command(() => AddCipherAsync()),
                Style = (Style)Application.Current.Resources["btn-primaryAccent"]
            };

            NoDataStackLayout = new StackLayout
            {
                Children = { noDataLabel, addCipherButton },
                VerticalOptions = LayoutOptions.CenterAndExpand,
                Padding = new Thickness(20, 0),
                Spacing = 20
            };

            SearchItem = new SearchToolBarItem(this);
            ToolbarItems.Add(SearchItem);

            ListView = new ExtendedListView(ListViewCachingStrategy.RecycleElement)
            {
                IsGroupingEnabled = true,
                ItemsSource = PresentationCiphersGroup,
                HasUnevenRows = true,
                GroupHeaderTemplate = new DataTemplate(() => new SectionHeaderViewCell(
                    nameof(Section<AutofillCipher>.Name))),
                ItemTemplate = new DataTemplate(() => new VaultListViewCell(
                    (VaultListPageModel.Cipher c) => Helpers.CipherMoreClickedAsync(this, c, true)))
            };

            if(Device.RuntimePlatform == Device.iOS)
            {
                ListView.RowHeight = -1;
            }

            Title = string.Format(AppResources.ItemsForUri, _name ?? "--");

            LoadingIndicator = new ActivityIndicator
            {
                IsRunning = true,
                VerticalOptions = LayoutOptions.CenterAndExpand,
                HorizontalOptions = LayoutOptions.Center
            };

            ContentView = new ContentView
            {
                Content = LoadingIndicator
            };

            var fabLayout = new FabLayout(ContentView);
            Fab = new Fab(fabLayout, "plus.png", async (sender, args) => await AddCipherAsync());
            ListView.BottomPadding = 170;

            Content = fabLayout;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ListView.ItemSelected += CipherSelected;
            SearchItem.InitEvents();
            _filterResultsCancellationTokenSource = FetchAndLoadVault();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ListView.ItemSelected -= CipherSelected;
            SearchItem.Dispose();
        }

        protected override bool OnBackButtonPressed()
        {
            GoogleAnalyticsService.TrackExtensionEvent("BackClosed", Uri.StartsWith("http") ? "Website" : "App");
            DeviceActionService.CloseAutofill();
            return true;
        }

        private void AdjustContent()
        {
            if(PresentationCiphersGroup.Count > 0)
            {
                ContentView.Content = ListView;
            }
            else
            {
                ContentView.Content = NoDataStackLayout;
            }
        }

        private CancellationTokenSource FetchAndLoadVault()
        {
            var cts = new CancellationTokenSource();
            _filterResultsCancellationTokenSource?.Cancel();

            Task.Run(async () =>
            {
                var autofillGroupings = new List<Section<AutofillCipher>>();
                var ciphers = await _cipherService.GetAllAsync(Uri);

                if(_appOptions.FillType.HasValue && _appOptions.FillType.Value != CipherType.Login)
                {
                    var others = ciphers?.Item3.Where(c => c.Type == _appOptions.FillType.Value)
                        .Select(c => new AutofillCipher(c, _appSettingsService, false))
                        .OrderBy(s => s.Name)
                        .ThenBy(s => s.Subtitle)
                        .ToList();
                    if(others?.Any() ?? false)
                    {
                        autofillGroupings.Add(new Section<AutofillCipher>(others, AppResources.Items));
                    }
                }
                else
                {
                    var normalLogins = ciphers?.Item1
                        .Select(l => new AutofillCipher(l, _appSettingsService, false))
                        .OrderBy(s => s.Name)
                        .ThenBy(s => s.Subtitle)
                        .ToList();
                    if(normalLogins?.Any() ?? false)
                    {
                        autofillGroupings.Add(new Section<AutofillCipher>(normalLogins,
                            AppResources.MatchingItems));
                    }

                    var fuzzyLogins = ciphers?.Item2
                        .Select(l => new AutofillCipher(l, _appSettingsService, true))
                        .OrderBy(s => s.Name)
                        .ThenBy(s => s.Subtitle)
                        .ToList();
                    if(fuzzyLogins?.Any() ?? false)
                    {
                        autofillGroupings.Add(new Section<AutofillCipher>(fuzzyLogins,
                            AppResources.PossibleMatchingItems));
                    }
                }

                Device.BeginInvokeOnMainThread(() =>
                {
                    if(autofillGroupings.Any())
                    {
                        PresentationCiphersGroup.ResetWithRange(autofillGroupings);
                    }

                    AdjustContent();
                });
            }, cts.Token);

            return cts;
        }

        private async void CipherSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var cipher = e.SelectedItem as AutofillCipher;
            if(cipher == null)
            {
                return;
            }

            if(_deviceInfoService.Version < 21)
            {
                Helpers.CipherMoreClickedAsync(this, cipher, true);
            }
            else
            {
                var autofillResponse = AppResources.Yes;
                if(cipher.Fuzzy)
                {
                    var options = new List<string> { AppResources.Yes };
                    if(cipher.Type == CipherType.Login && _connectivity.IsConnected)
                    {
                        options.Add(AppResources.YesAndSave);
                    }

                    autofillResponse = await DeviceActionService.DisplayAlertAsync(null,
                        string.Format(AppResources.BitwardenAutofillServiceMatchConfirm, _name), AppResources.No,
                        options.ToArray());
                }

                if(autofillResponse == AppResources.YesAndSave && cipher.Type == CipherType.Login)
                {
                    if(!_connectivity.IsConnected)
                    {
                        Helpers.AlertNoConnection(this);
                    }
                    else
                    {
                        var uris = cipher.CipherModel.Login?.Uris?.ToList();
                        if(uris == null)
                        {
                            uris = new List<LoginUri>();
                        }

                        uris.Add(new LoginUri
                        {
                            Uri = Uri.Encrypt(cipher.CipherModel.OrganizationId),
                            Match = null
                        });

                        cipher.CipherModel.Login.Uris = uris;

                        await DeviceActionService.ShowLoadingAsync(AppResources.Saving);
                        var saveTask = await _cipherService.SaveAsync(cipher.CipherModel);
                        await DeviceActionService.HideLoadingAsync();

                        if(saveTask.Succeeded)
                        {
                            GoogleAnalyticsService.TrackAppEvent("AddedLoginUriDuringAutofill");
                        }
                    }
                }

                if(autofillResponse == AppResources.Yes || autofillResponse == AppResources.YesAndSave)
                {
                    GoogleAnalyticsService.TrackExtensionEvent("AutoFilled",
                        Uri.StartsWith("http") ? "Website" : "App");
                    DeviceActionService.Autofill(cipher);
                }
            }

            ((ListView)sender).SelectedItem = null;
        }

        private async Task AddCipherAsync()
        {
            if(_appOptions.FillType.HasValue && _appOptions.FillType != CipherType.Login)
            {
                var pageForOther = new VaultAddCipherPage(_appOptions.FillType.Value, null, null, true);
                await Navigation.PushForDeviceAsync(pageForOther);
                return;
            }

            var pageForLogin = new VaultAddCipherPage(CipherType.Login, Uri, _name, true);
            await Navigation.PushForDeviceAsync(pageForLogin);
        }

        private class SearchToolBarItem : ExtendedToolbarItem
        {
            private readonly VaultAutofillListCiphersPage _page;

            public SearchToolBarItem(VaultAutofillListCiphersPage page)
            {
                _page = page;
                Text = AppResources.Search;
                Icon = "search.png";
                Priority = 1;
                ClickAction = () => DoClick();
            }

            private void DoClick()
            {
                _page.GoogleAnalyticsService.TrackExtensionEvent("CloseToSearch",
                    _page.Uri.StartsWith("http") ? "Website" : "App");
                Application.Current.MainPage = new ExtendedNavigationPage(new VaultListCiphersPage(uri: _page.Uri));
                _page.DeviceActionService.Toast(string.Format(AppResources.BitwardenAutofillServiceSearch, _page._name),
                    true);
            }
        }
    }
}
