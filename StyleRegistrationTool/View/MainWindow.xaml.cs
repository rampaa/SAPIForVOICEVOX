using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using SFVvCommon;
using StyleRegistrationTool.ViewModel;

namespace StyleRegistrationTool.View
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    internal sealed partial class MainWindow
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

#if x64
            const string bitStr = "64bit版";
#else
            const string bitStr = "32bit版";
#endif
            Title += bitStr;

            _viewModel = new MainViewModel(this);
            DataContext = _viewModel;
            Loaded += _viewModel.MainWindow_Loaded;
        }

        /// <summary>
        /// ウィンドウハンドルを取得します。
        /// </summary>
        public IntPtr Handle
        {
            get
            {
                WindowInteropHelper helper = new WindowInteropHelper(this);
                return helper.Handle;
            }
        }

        private void VoicevoxStyleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.VoicevoxStyle_SelectedItems = VoicevoxStyleList.SelectedItems.Cast<VoicevoxStyle>();
        }

        private void SapiStyleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SapiStyle_SelectedItems = SapiStyleList.SelectedItems.Cast<SapiStyle>();
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader columnHeader = (GridViewColumnHeader)sender;
            string columnTag = columnHeader.Tag.ToString();
            string columnHeaderString = columnHeader.Content.ToString();

            bool isAscending = !columnHeaderString.Contains("▼");

            IEnumerable<SapiStyle> sortedList;
            switch (columnTag)
            {
                case nameof(SapiStyle.AppName):
                    sortedList = isAscending ? _viewModel.SapiStyles.OrderBy(x => x.AppName) : _viewModel.SapiStyles.OrderByDescending(x => x.AppName);
                    break;
                case nameof(SapiStyle.Name):
                    sortedList = isAscending ? _viewModel.SapiStyles.OrderBy(x => x.Name, new StyleComparer()) : _viewModel.SapiStyles.OrderByDescending(x => x.Name, new StyleComparer());
                    break;
                case nameof(SapiStyle.StyleName):
                    sortedList = isAscending ? _viewModel.SapiStyles.OrderBy(x => x.StyleName) : _viewModel.SapiStyles.OrderByDescending(x => x.StyleName);
                    break;
                case nameof(SapiStyle.ID):
                    sortedList = isAscending ? _viewModel.SapiStyles.OrderBy(x => x.ID) : _viewModel.SapiStyles.OrderByDescending(x => x.ID);
                    break;
                case nameof(SapiStyle.Port):
                    sortedList = isAscending ? _viewModel.SapiStyles.OrderBy(x => x.Port) : _viewModel.SapiStyles.OrderByDescending(x => x.Port);
                    break;
                default:
                    return;
            }

            if (columnHeaderString.Contains("▼"))
            {
                columnHeaderString = columnHeaderString.Replace("▼", "▲");
            }
            else if (columnHeaderString.Contains("▲"))
            {
                columnHeaderString = columnHeaderString.Replace("▲", "▼");
            }
            else
            {
                if (isAscending)
                {
                    columnHeaderString += "▼";
                }
                else
                {
                    columnHeaderString += "▲";
                }
                columnHeader.Width += 10;
            }
            columnHeader.Content = columnHeaderString;
            //自分以外のヘッダーから▼マークを削除
            List<GridViewColumnHeader> columnHeaders = new List<GridViewColumnHeader> { AppNameHeader, NameHeader, StyleNameHeader, IDHeader, PortHeader };
            columnHeaders.Remove(columnHeader);
            foreach (GridViewColumnHeader item in columnHeaders)
            {
                string headerString = item.Content.ToString();
                headerString = headerString.Replace("▲", "");
                headerString = headerString.Replace("▼", "");
                item.Content = headerString;
            }

            _viewModel.SapiStyles = new ObservableCollection<SapiStyle>(sortedList);
        }
    }
}
