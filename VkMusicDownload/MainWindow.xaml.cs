using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using VkMusicDownload.VkHelpers;

namespace VkMusicDownload
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public AlbumResponse[] AllComposition { get; private set; }
        private readonly MediaPlayer _player = new MediaPlayer();
        public MainWindow()
        {
            InitializeComponent();
            webBrowser.Visibility = Visibility.Visible;
            webBrowser.Navigate(String.Format("https://oauth.vk.com/authorize?client_id={0}&scope={1}&redirect_uri={2}&display=page&response_type=token", ConfigurationSettings.AppSettings["VKAppId"], ConfigurationSettings.AppSettings["VKScope"], ConfigurationSettings.AppSettings["VKRedirectUri"]));

        }

        private string VkRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var response = (HttpWebResponse)request.GetResponse();
            var reader = new StreamReader(response.GetResponseStream());
            var responseText = reader.ReadToEnd();
            return responseText;
        }

        private void LoadUserCompositions(string userId)
        {

            var str = string.Format("https://api.vk.com/method/users.get?uids={0}",
             userId);

            var responseText = VkRequest(str);

            try
            {
                var users = JsonConvert.DeserializeObject<VkUsers>(responseText);
                var uid = users.response[0].uid;

                str = string.Format("https://api.vk.com/method/audio.get?uid={0}&access_token={1}",
                    uid, Vk.AccessToken);


                responseText = VkRequest(str);

                var album = JsonConvert.DeserializeObject<VkAlbum>(responseText);
                AllComposition = album.response;
                albumCompositions.ItemsSource = album.response;
                Title = String.Format("Музыка пользователя: <{0} {1}> загружена", users.response[0].first_name,
                    users.response[0].last_name);

            }
            catch (Exception) { }
        }

        private void LoadGroupCompositions(string groupId)
        {
            var str = string.Format("https://api.vk.com/method/groups.getById?gid={0}",
               groupId);

            var responseText = VkRequest(str);

            try
            {
                var groups = JsonConvert.DeserializeObject<VkGroup>(responseText);
                var uid = groups.response[0].gid;

                str = string.Format("https://api.vk.com/method/audio.get?gid={0}&access_token={1}",
                    uid, Vk.AccessToken);

                responseText = VkRequest(str);

                var album = JsonConvert.DeserializeObject<VkAlbum>(responseText);
                AllComposition = album.response;
                albumCompositions.ItemsSource = album.response;
                Title = String.Format("Музыка группы: <{0}> загружена", groups.response[0].name);
            }
            catch (Exception)
            {
            }
        }

        private void ButtonLoadCompositions(object sender, RoutedEventArgs e)
        {
            if (userName.Text == "" && groupName.Text == "")
            {
                LoadUserCompositions(Vk.UserId);
                return;
            }
            if (userName.Text != "")
            {
                LoadUserCompositions(userName.Text);
                return;
            }
            if (groupName.Text != "")
            {
                LoadGroupCompositions(groupName.Text);
            }

        }

        private void WebBrowserNavigated(object sender, NavigationEventArgs e)
        {
            var clearUriFragment = e.Uri.Fragment.Replace("#", "").Trim();
            var parameters = HttpUtility.ParseQueryString(clearUriFragment);
            Vk.AccessToken = parameters.Get("access_token");
            Vk.UserId = parameters.Get("user_id");
            if (Vk.AccessToken != null && Vk.UserId != null)
            {
                webBrowser.Visibility = Visibility.Hidden;
            }
        }


        private async Task DownloadCompositions(AlbumResponse composition, String path)
        {
            try
            {
                if (path[path.Length - 1] != '\\')
                {
                    path = path + "\\";
                }
                var fileName = composition.title;
                if (fileName.Length > 40)
                {
                    fileName = fileName.Substring(0, 40);
                }
                fileName = fileName.Replace(":", "").Replace("\\", "").Replace("/", "").Replace("*", "").Replace("?", "").Replace("\"", "");
                using (var client = new WebClient())
                {

                    client.DownloadProgressChanged += (o, args) =>
                    {
                        progressBar.Value = args.ProgressPercentage;
                    };


                    client.DownloadFileCompleted += (o, args) =>
                    {
                        progressBar.Value = 0;
                        compositionName.Text = "";
                    };
                    compositionName.Text = composition.title;
                    await client.DownloadFileTaskAsync(new Uri(composition.url), path + fileName + ".mp3");
                }
            }
            catch (Exception)
            {
            }
        }


        private async void ButtonSaveSelectCompositions(object sender, RoutedEventArgs e)
        {
            var selectedBuf = albumCompositions.SelectedItems;
            var selected = selectedBuf.Cast<AlbumResponse>().ToList();
            if (selected.Count > 0)
            {
                var dialog = new CommonOpenFileDialog { IsFolderPicker = true };
                var result = dialog.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    var path = dialog.FileName;
                    foreach (AlbumResponse composition in selected)
                    {
                        await DownloadCompositions(composition, path);
                    }
                }
            }
        }

        private async void AlbumCompositionsMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selected = (AlbumResponse)albumCompositions.SelectedItem;
            if (selected != null)
            {
                var dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                CommonFileDialogResult result = dialog.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    var path = dialog.FileName;
                    await DownloadCompositions(selected, path);
                }
            }

        }

        private void MenuExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void MenuSaveAllCompositions(object sender, RoutedEventArgs e)
        {
            var selectedBuf = albumCompositions.ItemsSource;
            var all = selectedBuf.Cast<AlbumResponse>().ToList();
            if (all.Any())
            {
                var dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                CommonFileDialogResult result = dialog.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    var path = dialog.FileName;
                    foreach (AlbumResponse composition in all)
                    {
                        await DownloadCompositions(composition, path);
                    }
                }
            }
        }

        private void FilterTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var str = filter.Text.ToLower();
            if (AllComposition != null && AllComposition.Any())
            {
                if (String.IsNullOrEmpty(str))
                {
                    albumCompositions.ItemsSource = AllComposition;
                    return;
                }
                albumCompositions.ItemsSource =
                    AllComposition.Where(
                        x =>
                            x.artist.ToLower().IndexOf(str, StringComparison.Ordinal) >= 0 ||
                            x.title.ToLower().IndexOf(str, StringComparison.Ordinal) > 0);
            }
        }

        private void AlbumCompositionsMouseRightPlayComposition(object sender, MouseButtonEventArgs e)
        {
            var selected = (AlbumResponse)albumCompositions.SelectedItem;
            if (selected != null)
            {
                _player.Open(new Uri(selected.url, UriKind.RelativeOrAbsolute));
                _player.Play();
                compositionName.Text = String.Format("Играет: {0}", selected.title);
                stopPlayer.IsEnabled = true;
            }
        }

        private void ButtonClickSopPlayer(object sender, RoutedEventArgs e)
        {
            _player.Stop();
            stopPlayer.IsEnabled = false;
        }

        private void MenuItemClickHelp(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow();
            helpWindow.Show();
        }

        private void UserNameKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (userName.Text != "")
                {
                    LoadUserCompositions(userName.Text);
                }
            }
        }

        private void GroupNameKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (groupName.Text != "")
                {
                    LoadGroupCompositions(groupName.Text);
                }
            }
        }

    }
}
