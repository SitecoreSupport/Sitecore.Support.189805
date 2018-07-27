namespace Sitecore.Support.Controllers
{
    using Sitecore;
    using Sitecore.Configuration;
    using Sitecore.Controllers;
    using Sitecore.Controllers.Results;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Data.Managers;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Mvc.Filters;
    using Sitecore.Pipelines;
    using Sitecore.Pipelines.Upload;
    using Sitecore.Resources.Media;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Net;
    using System.Web;
    using System.Web.Mvc;

    [SitecoreAuthorize]
    public class SupportMediaController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Upload(string database, string destinationUrl)
        {
            try
            {
                ID result;
                if (ID.TryParse(destinationUrl, out result))
                {
                    Item item = Factory.GetDatabase(database).GetItem(result);
                    destinationUrl = ((item != null) ? item.Paths.FullPath : destinationUrl);
                }
                if (base.Request?.Url != (Uri)null && !base.Request.Url.AbsolutePath.Contains("/sitecore/shell/"))
                {
                    return DoUploadSpeak(database, destinationUrl);
                }
                return DoUpload(database, destinationUrl);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex, this);
                SitecoreViewModelResult sitecoreViewModelResult = new SitecoreViewModelResult();
                sitecoreViewModelResult.Result.errorItems = new List<ErrorItem>
            {
                new ErrorItem("exception", string.Empty, "Internal server error.")
            };
                base.Response.StatusCode = new HttpStatusCodeResult(HttpStatusCode.NotFound).StatusCode;
                base.Response.TrySkipIisCustomErrors = true;
                foreach (dynamic item2 in sitecoreViewModelResult.Result.errorItems)
                {
                    Log.Error(string.Format("{0}, {1}, {2}", item2.Message, item2.Param, item2.Value), this);
                }
                return sitecoreViewModelResult;
            }
        }

        private static bool ValidateDestination(string database, string destinationUrl, SitecoreViewModelResult result)
        {
            List<ErrorItem> list = new List<ErrorItem>();
            bool flag = true;
            Database database2 = ClientHost.Databases.ContentDatabase;
            if (!string.IsNullOrEmpty(database))
            {
                database2 = Factory.GetDatabase(database);
            }
            Item item = database2.GetItem(destinationUrl);
            if (item == null)
            {
                list.Add(new ErrorItem("destination", destinationUrl, ClientHost.Globalization.Translate("Root item was not found")));
                Log.Error($"Root item wasn't found at: {destinationUrl}.", result);
                flag = false;
            }
            else if (!item.Access.CanCreate())
            {
                list.Add(new ErrorItem("destination", destinationUrl, ClientHost.Globalization.Translate("You do not have permission to upload files to the currently selected folder.")));
                Log.Error($"You do not have permission to upload files to the currently selected folder. {destinationUrl}", result);
                flag = false;
            }
            if (!flag)
            {
                result.Result.errorItems = list;
            }
            return flag;
        }

        private static bool ValidateFile(HttpPostedFileBase file, SitecoreViewModelResult result)
        {
            List<ErrorItem> list = new List<ErrorItem>();
            int contentLength = file.ContentLength;
            bool flag = true;
            if (contentLength > Settings.Media.MaxSizeInDatabase)
            {
                list.Add(new ErrorItem("size", contentLength.ToString(), string.Format(ClientHost.Globalization.Translate("The file exceeds the maximum size ({0})."), Settings.Media.MaxSizeInDatabase)));
                Log.Error(string.Format($"The file exceeds the maximum size ({Settings.Media.MaxSizeInDatabase}).", Array.Empty<object>()), result);
                flag = false;
            }
            if (!flag)
            {
                result.Result.errorItems = list;
            }
            return flag;
        }

        private string ParseDestinationUrl(string destinationUrl)
        {
            if (!destinationUrl.EndsWith("/"))
            {
                destinationUrl += "/";
            }
            return destinationUrl;
        }

        private JsonResult DoUpload(string database, string destinationUrl)
        {
            string text = string.Empty;
            if (string.IsNullOrEmpty(destinationUrl))
            {
                destinationUrl = "/sitecore/media library";
            }
            List<UploadedFileItem> list = new List<UploadedFileItem>();
            SitecoreViewModelResult sitecoreViewModelResult = new SitecoreViewModelResult();
            if (!ValidateDestination(database, destinationUrl, sitecoreViewModelResult))
            {
                base.Response.StatusCode = new HttpStatusCodeResult(HttpStatusCode.NotFound).StatusCode;
                base.Response.TrySkipIisCustomErrors = true;
                return sitecoreViewModelResult;
            }
            UploadArgs uploadArgs = new UploadArgs
            {
                Files = System.Web.HttpContext.Current.Request.Files,
                Destination = UploadDestination.Database,
                Folder = ParseDestinationUrl(destinationUrl),
                Overwrite = false,
                Unpack = false,
                Versioned = Settings.Media.UploadAsVersionableByDefault,
                Language = Language.Current,
                CloseDialogOnEnd = false
            };
            HttpPostedFileBase httpPostedFileBase = base.Request.Files[0];
            if (httpPostedFileBase != null)
            {
                text = Path.GetFileNameWithoutExtension(httpPostedFileBase.FileName);
                if (!string.IsNullOrEmpty(base.Request.Form["name"]))
                {
                    text = base.Request.Form["name"];
                }
                text = ItemUtil.ProposeValidItemName(text, "default");
            }
            PipelineFactory.GetPipeline("uiUpload").Start(uploadArgs);
            if (!string.IsNullOrEmpty(uploadArgs.ErrorText))
            {
                SitecoreViewModelResult sitecoreViewModelResult2 = new SitecoreViewModelResult();
                sitecoreViewModelResult2.Result.errorItems = new List<ErrorItem>
            {
                new ErrorItem("uiUpload", string.Empty, "An error occurred during executing the uiUpload pipeline."),
                new ErrorItem("uiUploadInnerMessage", string.Empty, uploadArgs.ErrorText)
            };
                return sitecoreViewModelResult2;
            }
            foreach (Item uploadedItem in uploadArgs.UploadedItems)
            {
                uploadedItem.Editing.BeginEdit();
                if (!string.IsNullOrEmpty(text))
                {
                    uploadedItem.Name = text;
                }
                if (!string.IsNullOrEmpty(base.Request.Form["description"]))
                {
                    uploadedItem["Description"] = base.Request.Form["description"];
                }
                if (!string.IsNullOrEmpty(base.Request.Form["alternate"]))
                {
                    uploadedItem["Alt"] = base.Request.Form["alternate"];
                }
                uploadedItem.Editing.EndEdit();
                Database database2 = Context.ContentDatabase;
                if (!string.IsNullOrEmpty(database))
                {
                    database2 = (Factory.GetDatabase(database) ?? database2);
                }
                MediaUrlOptions options = new MediaUrlOptions(130, 130, false)
                {
                    Thumbnail = true,
                    BackgroundColor = Color.Transparent,
                    Database = database2
                };
                string mediaUrl = MediaManager.GetMediaUrl(uploadedItem, options);
                list.Add(new UploadedFileItem(uploadedItem.Name, uploadedItem.ID.ToString(), uploadedItem.ID.ToShortID().ToString(), mediaUrl));
            }
            sitecoreViewModelResult.Result.uploadedFileItems = list;
            return sitecoreViewModelResult;
        }

        [Obsolete("Use DoUpload with pipeline inside", false)]
        private JsonResult DoUploadSpeak(string database, string destinationUrl)
        {
            if (string.IsNullOrEmpty(destinationUrl))
            {
                destinationUrl = "/sitecore/media library";
            }
            List<UploadedFileItem> list = new List<UploadedFileItem>();
            SitecoreViewModelResult sitecoreViewModelResult = new SitecoreViewModelResult();
            if (!ValidateDestination(database, destinationUrl, sitecoreViewModelResult))
            {
                return sitecoreViewModelResult;
            }
            foreach (string file in base.Request.Files)
            {
                HttpPostedFileBase httpPostedFileBase = base.Request.Files[file];
                if (httpPostedFileBase != null)
                {
                    string name2 = Path.GetFileNameWithoutExtension(httpPostedFileBase.FileName);
                    if (!string.IsNullOrEmpty(base.Request.Form["name"]))
                    {
                        name2 = base.Request.Form["name"];
                    }
                    name2 = ItemUtil.ProposeValidItemName(name2, "default");
                    string alternateText = string.Empty;
                    if (!string.IsNullOrEmpty(base.Request.Form["alternate"]))
                    {
                        alternateText = base.Request.Form["alternate"];
                    }
                    Database database2 = Context.ContentDatabase;
                    if (!string.IsNullOrEmpty(database))
                    {
                        database2 = Factory.GetDatabase(database);
                    }
                    if (database2 == null)
                    {
                        database2 = Context.ContentDatabase;
                    }
                    MediaCreatorOptions options = new MediaCreatorOptions
                    {
                        AlternateText = alternateText,
                        Database = database2,
                        FileBased = Settings.Media.UploadAsFiles,
                        IncludeExtensionInItemName = Settings.Media.IncludeExtensionsInItemNames,
                        OverwriteExisting = true,
                        Language = LanguageManager.DefaultLanguage,
                        Versioned = Settings.Media.UploadAsVersionableByDefault,
                        Destination = ParseDestinationUrl(destinationUrl) + name2
                    };
                    if (!ValidateFile(httpPostedFileBase, sitecoreViewModelResult))
                    {
                        return sitecoreViewModelResult;
                    }
                    Item item = MediaManager.Creator.CreateFromStream(httpPostedFileBase.InputStream, "/upload/" + httpPostedFileBase.FileName, options);
                    if (!string.IsNullOrEmpty(base.Request.Form["description"]))
                    {
                        item.Editing.BeginEdit();
                        item["Description"] = base.Request.Form["description"];
                        item.Editing.EndEdit();
                    }
                    MediaItem mediaItem = new MediaItem(item);
                    MediaUrlOptions options2 = new MediaUrlOptions(130, 130, false)
                    {
                        Thumbnail = true,
                        BackgroundColor = Color.Transparent,
                        Database = mediaItem.Database
                    };
                    string mediaUrl = MediaManager.GetMediaUrl(mediaItem, options2);
                    list.Add(new UploadedFileItem(item.Name, item.ID.ToString(), item.ID.ToShortID().ToString(), mediaUrl));
                }
            }
            sitecoreViewModelResult.Result.uploadedFileItems = list;
            return sitecoreViewModelResult;
        }
    }
}