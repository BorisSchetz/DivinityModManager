﻿using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reactive.Disposables;
using DynamicData;
using DynamicData.Binding;
using System.Diagnostics;
using System.Globalization;
using static AlertBarWpf.AlertBarWpf;
using AutoUpdaterDotNET;
using System.Windows.Threading;
using System.Reactive.Concurrency;
using AdonisUI.Controls;
using System.Reactive;

namespace DivinityModManager.Views
{
#if debug
	public class MainWindowDebugData : MainWindowViewModel
	{
		public MainWindowDebugData () : base()
		{
			Random rnd = new Random();
			for(var i = 0; i < 60; i++)
			{
				var d = new DivinityModData()
				{
					Name = "Test" + i,
					Author = "LaughingLeader",
					Version = new DivinityModVersion(370871668),
					Description = "Test",
					UUID = DivinityModDataLoader.CreateHandle()
				};
				d.IsEditorMod = i%2 == 0;
				d.IsActive = rnd.Next(4) <= 2;
				this.AddMods(d);
			}

			for (var i = 0; i < 4; i++)
			{
				var p = new DivinityProfileData()
				{
					Name = "Profile" + i
				};
				p.ModOrder.AddRange(Mods.Select(m => m.UUID));
				Profiles.Add(p);
			}
			SelectedProfileIndex = 0;

			for (var i = 0; i < 4; i++)
			{
				var lo = new DivinityLoadOrder()
				{
					Name = "SavedOrder" + i
				};
				var orderList = this.mods.Items.Where(m => m.IsActive).Select(m => m.ToOrderEntry()).OrderBy(e => rnd.Next(10));
				lo.SetOrder(orderList);
				ModOrderList.Add(lo);
			}
			SelectedModOrderIndex = 2;
		}
	}
#endif

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : AdonisWindow, IViewFor<MainWindowViewModel>
	{
		private static MainWindow self;
		public static MainWindow Self => self;

		private TextWriterTraceListener debugLogListener;

		private ConflictCheckerWindow conflictCheckerWindow;
		public ConflictCheckerWindow ConflictCheckerWindow => conflictCheckerWindow;

		private SettingsWindow settingsWindow;
		public SettingsWindow SettingsWindow => settingsWindow;

		private AboutWindow aboutWindow;
		public AboutWindow AboutWindow => aboutWindow;

		public MainWindowViewModel ViewModel { get; set; }
		object IViewFor.ViewModel { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			self = this;

			settingsWindow = new SettingsWindow();
			SettingsWindow.OnWorkshopPathChanged += delegate
			{
				RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), _ => ViewModel.LoadWorkshopMods());
			};
			SettingsWindow.Hide();

			ViewModel = new MainWindowViewModel();
			this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

			AlertBar.Show += AlertBar_Show;

			AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;
#if DEBUG
			AutoUpdater.ReportErrors = true;
#endif
			AutoUpdater.HttpUserAgent = "DivinityModManagerUser";

			this.OneWayBind(ViewModel,
				viewModel => viewModel.Title,
				view => view.Title).DisposeWith(ViewModel.Disposables);

			ViewModel.CheckForAppUpdatesCommand = ReactiveCommand.Create(() => 
			{
#if !DEBUG
				AutoUpdater.ReportErrors = true;
#endif
				AutoUpdater.Start(DivinityApp.URL_UPDATE);
				ViewModel.Settings.LastUpdateCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
				ViewModel.SaveSettings();
#if !DEBUG
				Task.Delay(1000).ContinueWith(_ =>
				{
					AutoUpdater.ReportErrors = false;
				});
#endif
			});

			var c = ReactiveCommand.Create(() =>
			{
				if (!SettingsWindow.IsVisible)
				{
					SettingsWindow.Init(this.ViewModel.Settings);
					SettingsWindow.Show();
					settingsWindow.Owner = this;
				}
				else
				{
					SettingsWindow.Hide();
				}
			});
			c.ThrownExceptions.Subscribe((ex) =>
			{
				Trace.WriteLine("Error opening settings window: " + ex.ToString());
			});
			ViewModel.OpenPreferencesCommand = c;

			ViewModel.OpenAboutWindowCommand = ReactiveCommand.Create(() =>
			{
				if (AboutWindow == null)
				{
					aboutWindow = new AboutWindow();
				}

				if (!AboutWindow.IsVisible)
				{
					AboutWindow.DataContext = ViewModel;
					AboutWindow.Show();
					AboutWindow.Owner = this;
				}
				else
				{
					AboutWindow.Hide();
				}
			});

			this.WhenAnyValue(x => x.ViewModel.MainProgressIsActive).Subscribe((b) =>
			{
				if (b)
				{
					this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
				}
				else
				{
					this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
				}
			}).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.MainProgressValue, view => view.TaskbarItemInfo.ProgressValue).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.AddOrderConfigCommand, view => view.FileAddNewOrderMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.SaveOrderCommand, view => view.FileSaveOrderMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.SaveOrderAsCommand, view => view.FileSaveOrderAsMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.ExportOrderCommand, view => view.FileExportOrderToGameMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.ExportLoadOrderAsArchiveCommand, view => view.FileExportOrderToArchiveMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.ExportLoadOrderAsArchiveToFileCommand, view => view.FileExportOrderToArchiveAsMenuItem.Command).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.RefreshCommand, view => view.FileRefreshMenuItem.Command).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.OpenPreferencesCommand, view => view.SettingsPreferencesMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.ToggleDarkModeCommand, view => view.SettingsDarkModeMenuItem.Command).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.CheckForAppUpdatesCommand, view => view.HelpCheckForUpdateMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenDonationPageCommand, view => view.HelpDonationMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenRepoPageCommand, view => view.HelpOpenRepoPageMenuItem.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenAboutWindowCommand, view => view.HelpOpenAboutWindowMenuItem.Command).DisposeWith(ViewModel.Disposables);

			var res = this.TryFindResource("ModUpdaterPanel");
			if(res != null && res is ModUpdatesLayout modUpdaterPanel)
			{
				Binding binding = new Binding("ModUpdatesViewData");
				binding.Source = ViewModel;
				modUpdaterPanel.SetBinding(ModUpdatesLayout.DataContextProperty, binding);
			}

			//this.OneWayBind(ViewModel, vm => vm, view => view.DataContext).DisposeWith(disposableRegistration);
			//this.OneWayBind(ViewModel, vm => vm, view => view.LayoutContent.Content).DisposeWith(disposableRegistration);

			/*
			this.OneWayBind(ViewModel, vm => vm.SaveOrderCommand, view => view.SaveButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.SaveOrderAsCommand, view => view.SaveAsButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.ExportOrderCommand, view => view.ExportToModSettingsButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.RefreshCommand, view => view.RefreshButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenModsFolderCommand, view => view.OpenModsFolderButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenWorkshopFolderCommand, view => view.OpenWorkshopFolderButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenDOS2GameCommand, view => view.OpenDOS2GameButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenDonationPageCommand, view => view.OpenDonationPageButton.Command).DisposeWith(ViewModel.Disposables);
			this.OneWayBind(ViewModel, vm => vm.OpenRepoPageCommand, view => view.OpenRepoPageButton.Command).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.AddOrderConfigCommand, view => view.AddNewOrderButton.Command).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.Profiles, view => view.ProfilesComboBox.ItemsSource).DisposeWith(ViewModel.Disposables);
			this.Bind(ViewModel, vm => vm.SelectedProfileIndex, view => view.ProfilesComboBox.SelectedIndex).DisposeWith(ViewModel.Disposables);

			this.OneWayBind(ViewModel, vm => vm.ModOrderList, view => view.OrdersComboBox.ItemsSource).DisposeWith(ViewModel.Disposables);
			this.Bind(ViewModel, vm => vm.SelectedModOrderIndex, view => view.OrdersComboBox.SelectedIndex).DisposeWith(ViewModel.Disposables);
			*/

			//Menu Items
			//this.OneWayBind(ViewModel, vm => vm.OpenConflictCheckerCommand, view => view.ConflictCheckerMenuItem.Command).DisposeWith(ViewModel.Disposables);

			DataContext = ViewModel;

			this.WhenActivated(d =>
			{
				ViewModel.OnViewActivated(this);

				if(ViewModel.Settings.CheckForUpdates)
				{
					if (ViewModel.Settings.LastUpdateCheck == -1 || (DateTimeOffset.Now.ToUnixTimeSeconds() - ViewModel.Settings.LastUpdateCheck >= 43200))
					{
						AutoUpdater.Start(DivinityApp.URL_UPDATE);
					}
				}
			});
		}

		private void AutoUpdater_ApplicationExitEvent()
		{
			ViewModel.Settings.LastUpdateCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
			ViewModel.SaveSettings();
			App.Current.Shutdown();
		}

		private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
		{
			if (depObj != null)
			{
				for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
				{
					DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
					if (child != null && child is T)
					{
						yield return (T)child;
					}

					foreach (T childOfChild in FindVisualChildren<T>(child))
					{
						yield return childOfChild;
					}
				}
			}
		}

		private void AlertBar_Show(object sender, RoutedEventArgs e)
		{
			var spStandard = (StackPanel)AlertBar.FindName("spStandard");
			var spOutline = (StackPanel)AlertBar.FindName("spOutline");

			Grid grdParent;
			switch (AlertBar.Theme)
			{
				case ThemeType.Standard:
					grdParent = FindVisualChildren<Grid>(spStandard).FirstOrDefault();
					break;
				case ThemeType.Outline:
				default:
					grdParent = FindVisualChildren<Grid>(spOutline).FirstOrDefault();
					break;
			}

			TextBlock lblMessage = FindVisualChildren<TextBlock>(grdParent).FirstOrDefault();
			if(lblMessage != null)
			{
				Trace.WriteLine(lblMessage);
			}
		}

		public void ToggleConflictChecker(bool openWindow)
		{
			if(openWindow)
			{
				if (ConflictCheckerWindow == null)
				{
					conflictCheckerWindow = new ConflictCheckerWindow();
				}
				
				if(!conflictCheckerWindow.IsVisible)
				{
					conflictCheckerWindow.Init(ViewModel);
					conflictCheckerWindow.Show();
				}
			}
			else
			{
				if (conflictCheckerWindow != null)
				{
					conflictCheckerWindow.Close();
					conflictCheckerWindow = null;
				}
			}
		}

		private void ExportTestButton_Click(object sender, RoutedEventArgs e)
		{
			//DivinityModDataLoader.ExportModOrder(Data.ActiveModOrder, Data.Mods);
		}

		private void ComboBox_KeyDown_LoseFocus(object sender, KeyEventArgs e)
		{
			if((e.Key == Key.Enter || e.Key == Key.Return))
			{
				UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;
				elementWithFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
				e.Handled = true;
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void ProfileComboBox_OnUserClick(object sender, MouseButtonEventArgs e)
		{
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(200), () =>
			{
				if (ViewModel.Settings != null && ViewModel.Settings.LastOrder != ViewModel.SelectedModOrder.Name)
				{
					ViewModel.Settings.LastOrder = ViewModel.SelectedModOrder.Name;
					ViewModel.SaveSettings();
				}
			});
		}
	}
}