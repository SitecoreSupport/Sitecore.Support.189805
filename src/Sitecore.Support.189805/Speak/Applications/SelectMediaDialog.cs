namespace Sitecore.Support.Speak.Applications
{
    using Sitecore;
    using Sitecore.Data.Items;
    using Sitecore.Web;
    using System.Web;

    public class SelectMediaDialog : Sitecore.Speak.Applications.SelectMediaDialog
    {
        public override void Initialize()
        {
            RedirectOnItembucketsDisabled(ClientHost.Items.GetItem("{16227E67-F9CB-4FB7-9928-7FF6A529708E}"));
            string queryString = WebUtil.GetQueryString("ro");
            string queryString2 = WebUtil.GetQueryString("fo");
            bool showFullPath = GetShowFullPath(queryString2);
            string queryString3 = WebUtil.GetQueryString("hasUploaded");
            if (!string.IsNullOrEmpty(queryString3) && queryString3 == "1")
            {
                DataSource.Parameters["SearchConfigItemId"] = "{1E723604-BFE0-47F6-B7C5-3E2FA6DD70BD}";
                Menu.Parameters["DefaultSelectedItemId"] = "{BE8CD31C-2A01-4ED6-9C83-E84C2275E429}";
            }
            Item mediaItemFromQueryString = GetMediaItemFromQueryString(queryString);
            mediaItemFromQueryString = GetRootItem(mediaItemFromQueryString);
            Item mediaItemFromQueryString2 = GetMediaItemFromQueryString(queryString2);
            if (mediaItemFromQueryString != null)
            {
                DataSource.Parameters["RootItemId"] = mediaItemFromQueryString.ID.ToString();
            }
            if (mediaItemFromQueryString == null)
            {
                mediaItemFromQueryString = ClientHost.Items.GetItem("/sitecore/media library");
            }
            MediaResultsListControl.Parameters["ContentLanguage"] = mediaItemFromQueryString.Language.ToString();
            MediaResultsListControl.Parameters["DefaultSelectedItemId"] = ((mediaItemFromQueryString2 == null) ? mediaItemFromQueryString.ID.ToString() : mediaItemFromQueryString2.ID.ToString());
            MediaFolderValueText.Parameters["Text"] = GetDisplayPath(mediaItemFromQueryString.Paths.Path, null, showFullPath);
            TreeViewToggleButton.Parameters["Click"] = string.Format(TreeViewToggleButton.Parameters["Click"], HttpUtility.UrlEncode(queryString), HttpUtility.UrlEncode(queryString2), showFullPath);
            string url;
            string before;
            string after;
            string separator;
            FillCommandParts(UploadButton.Parameters["Click"], out url, out before, out after, out separator);
            string text = SetUrlContentDatabase(url, WebUtil.GetQueryString("sc_content"));
            string format = before + separator + text + separator + after;
            UploadButton.Parameters["Click"] = string.Format(format, HttpUtility.UrlEncode(queryString), HttpUtility.UrlEncode(queryString2), showFullPath);
        }
    }
}