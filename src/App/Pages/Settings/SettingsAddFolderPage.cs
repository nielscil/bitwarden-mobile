﻿using System;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Models;
using Bit.App.Resources;
using Plugin.Connectivity.Abstractions;
using Xamarin.Forms;
using XLabs.Ioc;
using System.Linq;
using Bit.App.Utilities;

namespace Bit.App.Pages
{
    public class SettingsAddFolderPage : ExtendedContentPage
    {
        private readonly IFolderService _folderService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IConnectivity _connectivity;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;
        private DateTime? _lastAction;

        public SettingsAddFolderPage()
        {
            _folderService = Resolver.Resolve<IFolderService>();
            _deviceActionService = Resolver.Resolve<IDeviceActionService>();
            _connectivity = Resolver.Resolve<IConnectivity>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();

            Init();
        }

        public FormEntryCell NameCell { get; set; }

        private void Init()
        {
            NameCell = new FormEntryCell(AppResources.Name);

            var table = new ExtendedTableView
            {
                Intent = TableIntent.Settings,
                EnableScrolling = false,
                HasUnevenRows = true,
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        NameCell
                    }
                }
            };

            if(Device.RuntimePlatform == Device.iOS)
            {
                table.RowHeight = -1;
                table.EstimatedRowHeight = 70;
            }
            else if(Device.RuntimePlatform == Device.Android)
            {
                table.BottomPadding = 50;
            }

            var saveToolBarItem = new ToolbarItem(AppResources.Save, Helpers.ToolbarImage("envelope.png"), async () =>
            {
                if(_lastAction.LastActionWasRecent())
                {
                    return;
                }
                _lastAction = DateTime.UtcNow;

                if(!_connectivity.IsConnected)
                {
                    AlertNoConnection();
                    return;
                }

                if(string.IsNullOrWhiteSpace(NameCell.Entry.Text))
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired,
                        AppResources.Name), AppResources.Ok);
                    return;
                }

                var folder = new Folder
                {
                    Name = NameCell.Entry.Text.Encrypt()
                };

                await _deviceActionService.ShowLoadingAsync(AppResources.Saving);
                var saveResult = await _folderService.SaveAsync(folder);
                await _deviceActionService.HideLoadingAsync();

                if(saveResult.Succeeded)
                {
                    _deviceActionService.Toast(AppResources.FolderCreated);
                    _googleAnalyticsService.TrackAppEvent("CreatedFolder");
                    await Navigation.PopForDeviceAsync();
                }
                else if(saveResult.Errors.Count() > 0)
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, saveResult.Errors.First().Message, AppResources.Ok);
                }
                else
                {
                    await DisplayAlert(null, AppResources.AnErrorHasOccurred, AppResources.Ok);
                }
            }, ToolbarItemOrder.Default, 0);

            Title = AppResources.AddFolder;
            Content = table;
            ToolbarItems.Add(saveToolBarItem);
            if(Device.RuntimePlatform == Device.iOS || Device.RuntimePlatform == Device.UWP)
            {
                ToolbarItems.Add(new DismissModalToolBarItem(this, AppResources.Cancel));
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            NameCell.InitEvents();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            NameCell.Dispose();
        }

        private void AlertNoConnection()
        {
            DisplayAlert(AppResources.InternetConnectionRequiredTitle, AppResources.InternetConnectionRequiredMessage, AppResources.Ok);
        }
    }
}
