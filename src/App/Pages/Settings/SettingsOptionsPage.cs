﻿using System;
using Bit.App.Abstractions;
using Bit.App.Resources;
using Xamarin.Forms;
using XLabs.Ioc;
using Bit.App.Controls;
using Plugin.Settings.Abstractions;
using Bit.App.Utilities;

namespace Bit.App.Pages
{
    public class SettingsOptionsPage : ExtendedContentPage
    {
        private readonly ISettings _settings;
        private readonly IAppSettingsService _appSettings;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;

        public SettingsOptionsPage()
        {
            _settings = Resolver.Resolve<ISettings>();
            _appSettings = Resolver.Resolve<IAppSettingsService>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();

            Init();
        }

        private RedrawableStackLayout StackLayout { get; set; }
        private ExtendedSwitchCell CopyTotpCell { get; set; }
        private Label CopyTotpLabel { get; set; }
        private ExtendedSwitchCell AnalyticsCell { get; set; }
        private Label AnalyticsLabel { get; set; }
        private ExtendedSwitchCell WebsiteIconsCell { get; set; }
        private Label WebsiteIconsLabel { get; set; }
        private ExtendedSwitchCell AutofillPersistNotificationCell { get; set; }
        private Label AutofillPersistNotificationLabel { get; set; }
        private ExtendedSwitchCell AutofillPasswordFieldCell { get; set; }
        private Label AutofillPasswordFieldLabel { get; set; }
        private ExtendedSwitchCell AutofillAlwaysCell { get; set; }
        private Label AutofillAlwaysLabel { get; set; }

        private void Init()
        {
            WebsiteIconsCell = new ExtendedSwitchCell
            {
                Text = AppResources.DisableWebsiteIcons,
                On = _appSettings.DisableWebsiteIcons
            };

            var websiteIconsTable = new FormTableView(this, true)
            {
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        WebsiteIconsCell
                    }
                }
            };

            CopyTotpCell = new ExtendedSwitchCell
            {
                Text = AppResources.DisableAutoTotpCopy,
                On = _settings.GetValueOrDefault(Constants.SettingDisableTotpCopy, false)
            };

            var totpTable = new FormTableView(this)
            {
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        CopyTotpCell
                    }
                }
            };

            AnalyticsCell = new ExtendedSwitchCell
            {
                Text = AppResources.DisableGA,
                On = _settings.GetValueOrDefault(Constants.SettingGaOptOut, false)
            };

            var analyticsTable = new FormTableView(this)
            {
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        AnalyticsCell
                    }
                }
            };

            CopyTotpLabel = new FormTableLabel(this)
            {
                Text = AppResources.DisableAutoTotpCopyDescription
            };

            AnalyticsLabel = new FormTableLabel(this)
            {
                Text = AppResources.DisableGADescription
            };

            WebsiteIconsLabel = new FormTableLabel(this)
            {
                Text = AppResources.DisableWebsiteIconsDescription
            };

            StackLayout = new RedrawableStackLayout
            {
                Children =
                {
                    websiteIconsTable, WebsiteIconsLabel,
                    totpTable, CopyTotpLabel,
#if !FDROID
                    analyticsTable, AnalyticsLabel
#endif
                },
                Spacing = 0
            };

            if(Device.RuntimePlatform == Device.Android)
            {
                AutofillAlwaysCell = new ExtendedSwitchCell
                {
                    Text = AppResources.AutofillAlways,
                    On = !_appSettings.AutofillPersistNotification && !_appSettings.AutofillPasswordField
                };

                var autofillAlwaysTable = new FormTableView(this, true)
                {
                    Root = new TableRoot
                    {
                        new TableSection(AppResources.AutofillAccessibilityService)
                        {
                            AutofillAlwaysCell
                        }
                    }
                };

                AutofillAlwaysLabel = new FormTableLabel(this)
                {
                    Text = AppResources.AutofillAlwaysDescription
                };

                AutofillPersistNotificationCell = new ExtendedSwitchCell
                {
                    Text = AppResources.AutofillPersistNotification,
                    On = _appSettings.AutofillPersistNotification
                };

                var autofillPersistNotificationTable = new FormTableView(this)
                {
                    Root = new TableRoot
                    {
                        new TableSection(Helpers.GetEmptyTableSectionTitle())
                        {
                            AutofillPersistNotificationCell
                        }
                    }
                };

                AutofillPersistNotificationLabel = new FormTableLabel(this)
                {
                    Text = AppResources.AutofillPersistNotificationDescription
                };

                AutofillPasswordFieldCell = new ExtendedSwitchCell
                {
                    Text = AppResources.AutofillPasswordField,
                    On = _appSettings.AutofillPasswordField
                };

                var autofillPasswordFieldTable = new FormTableView(this)
                {
                    Root = new TableRoot
                    {
                        new TableSection(Helpers.GetEmptyTableSectionTitle())
                        {
                            AutofillPasswordFieldCell
                        }
                    }
                };

                AutofillPasswordFieldLabel = new FormTableLabel(this)
                {
                    Text = AppResources.AutofillPasswordFieldDescription
                };

                StackLayout.Children.Add(autofillAlwaysTable);
                StackLayout.Children.Add(AutofillAlwaysLabel);
                StackLayout.Children.Add(autofillPasswordFieldTable);
                StackLayout.Children.Add(AutofillPasswordFieldLabel);
                StackLayout.Children.Add(autofillPersistNotificationTable);
                StackLayout.Children.Add(AutofillPersistNotificationLabel);
            }

            var scrollView = new ScrollView
            {
                Content = StackLayout
            };

            if(Device.RuntimePlatform == Device.iOS || Device.RuntimePlatform == Device.UWP)
            {
                analyticsTable.RowHeight = websiteIconsTable.RowHeight = totpTable.RowHeight = -1;
                analyticsTable.EstimatedRowHeight = websiteIconsTable.EstimatedRowHeight =
                    totpTable.EstimatedRowHeight = 70;
                ToolbarItems.Add(new DismissModalToolBarItem(this, AppResources.Close));
            }

            Title = AppResources.Options;
            Content = scrollView;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            AnalyticsCell.OnChanged += AnalyticsCell_Changed;
            WebsiteIconsCell.OnChanged += WebsiteIconsCell_Changed;
            CopyTotpCell.OnChanged += CopyTotpCell_OnChanged;

            if(Device.RuntimePlatform == Device.Android)
            {
                AutofillAlwaysCell.OnChanged += AutofillAlwaysCell_OnChanged;
                AutofillPasswordFieldCell.OnChanged += AutofillPasswordFieldCell_OnChanged;
                AutofillPersistNotificationCell.OnChanged += AutofillPersistNotificationCell_OnChanged;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            AnalyticsCell.OnChanged -= AnalyticsCell_Changed;
            WebsiteIconsCell.OnChanged -= WebsiteIconsCell_Changed;
            CopyTotpCell.OnChanged -= CopyTotpCell_OnChanged;

            if(Device.RuntimePlatform == Device.Android)
            {
                AutofillAlwaysCell.OnChanged -= AutofillAlwaysCell_OnChanged;
                AutofillPasswordFieldCell.OnChanged -= AutofillPasswordFieldCell_OnChanged;
                AutofillPersistNotificationCell.OnChanged -= AutofillPersistNotificationCell_OnChanged;
            }
        }

        private void WebsiteIconsCell_Changed(object sender, ToggledEventArgs e)
        {
            var cell = sender as ExtendedSwitchCell;
            if(cell == null)
            {
                return;
            }

            _appSettings.DisableWebsiteIcons = cell.On;
        }

        private void AnalyticsCell_Changed(object sender, ToggledEventArgs e)
        {
            var cell = sender as ExtendedSwitchCell;
            if(cell == null)
            {
                return;
            }

            _settings.AddOrUpdateValue(Constants.SettingGaOptOut, cell.On);
            _googleAnalyticsService.SetAppOptOut(cell.On);
        }

        private void CopyTotpCell_OnChanged(object sender, ToggledEventArgs e)
        {
            var cell = sender as ExtendedSwitchCell;
            if(cell == null)
            {
                return;
            }

            _settings.AddOrUpdateValue(Constants.SettingDisableTotpCopy, cell.On);
        }

        private void AutofillAlwaysCell_OnChanged(object sender, ToggledEventArgs e)
        {
            var cell = sender as ExtendedSwitchCell;
            if(cell == null)
            {
                return;
            }

            if(cell.On)
            {
                AutofillPasswordFieldCell.On = false;
                AutofillPersistNotificationCell.On = false;
                _appSettings.AutofillPersistNotification = false;
                _appSettings.AutofillPasswordField = false;
            }
        }

        private void AutofillPersistNotificationCell_OnChanged(object sender, ToggledEventArgs e)
        {
            var cell = sender as ExtendedSwitchCell;
            if(cell == null)
            {
                return;
            }

            _appSettings.AutofillPersistNotification = cell.On;
            if(cell.On)
            {
                AutofillPasswordFieldCell.On = false;
                AutofillAlwaysCell.On = false;
            }
        }

        private void AutofillPasswordFieldCell_OnChanged(object sender, ToggledEventArgs e)
        {
            var cell = sender as ExtendedSwitchCell;
            if(cell == null)
            {
                return;
            }

            _appSettings.AutofillPasswordField = cell.On;
            if(cell.On)
            {
                AutofillPersistNotificationCell.On = false;
                AutofillAlwaysCell.On = false;
            }
        }

        private class FormTableView : ExtendedTableView
        {
            public FormTableView(SettingsOptionsPage page, bool header = false)
            {
                Intent = TableIntent.Settings;
                EnableScrolling = false;
                HasUnevenRows = true;
                EnableSelection = true;
                VerticalOptions = LayoutOptions.Start;
                NoFooter = true;
                NoHeader = !header;
                WrappingStackLayout = () => page.StackLayout;
            }
        }

        private class FormTableLabel : Label
        {
            public FormTableLabel(Page page)
            {
                LineBreakMode = LineBreakMode.WordWrap;
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label));
                Style = (Style)Application.Current.Resources["text-muted"];
                Margin = new Thickness(15, (page.IsLandscape() ? 5 : 0), 15, 25);
            }
        }
    }
}
