namespace Sitecore.Support.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Net;
    using System.Web;
    using System.Web.Mvc;
    using Sitecore.Configuration;
    using Sitecore.Controllers.Results;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Data.Managers;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Mvc;
    using Sitecore.Names;
    using Sitecore.Pipelines;
    using Sitecore.Pipelines.Upload;
    using Sitecore.Resources.Media;
    using Sitecore.Web;
    using Sitecore.Controllers;

    /// <summary>
    /// Defines the <see cref="SupportMediaController"/> class.
    /// </summary>
    [Authorize]
    public class SupportMediaController : Controller
    {
        #region Public Methods and Operators

        /// <summary>
        /// Uploads this instance.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="destinationUrl">The destination URL.</param>
        /// <returns>The result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [NotNull]
        public JsonResult Upload(string database, [CanBeNull] string destinationUrl)
        {
            try
            {
                ID destinationId;

                if (ID.TryParse(destinationUrl, out destinationId))
                {
                    var db = Factory.GetDatabase(database);
                    var itemDestination = db.GetItem(destinationId);
                    destinationUrl = itemDestination != null ? itemDestination.Paths.FullPath : destinationUrl;
                }

                if (this.Request?.Url != null && !this.Request.Url.AbsolutePath.Contains("/sitecore/shell/"))
                {
                    return this.DoUploadSpeak(database, destinationUrl);
                }
                return this.DoUpload(database, destinationUrl);
            }
            catch (Exception e)
            {
                Log.Error(e.Message, e, this);

                var errorResult = new SitecoreViewModelResult();
                errorResult.Result.errorItems = new List<ErrorItem>
            {
                new ErrorItem("exception", string.Empty, Texts.InternalServerError)
            };

                this.Response.StatusCode = new HttpStatusCodeResult(HttpStatusCode.NotFound).StatusCode;
                this.Response.TrySkipIisCustomErrors = true;
                foreach (var errorItem in errorResult.Result.errorItems)
                {
                    Log.Error(string.Format("{0}, {1}, {2}", errorItem.Message, errorItem.Param, errorItem.Value), this);
                }

                return errorResult;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// The validate destination.
        /// </summary>
        /// <param name="database">
        /// The database.
        /// </param>
        /// <param name="destinationUrl">
        /// The destination url.
        /// </param>
        /// <param name="result">
        /// The result.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private static bool ValidateDestination(string database, string destinationUrl, SitecoreViewModelResult result)
        {
            var errorItems = new List<ErrorItem>();
            var isValid = true;

            var uploadDatabase = ClientHost.Databases.ContentDatabase;

            if (!string.IsNullOrEmpty(database))
            {
                uploadDatabase = Factory.GetDatabase(database);
            }

            var item = uploadDatabase.GetItem(destinationUrl);
            if (item == null)
            {
                errorItems.Add(new ErrorItem("destination", destinationUrl, ClientHost.Globalization.Translate(Texts.NoRootItemFound)));
                Log.Error(string.Format("Root item wasn't found at: {0}.", destinationUrl), result);
                isValid = false;
            }
            else if (!item.Access.CanCreate())
            {
                errorItems.Add(new ErrorItem("destination", destinationUrl, ClientHost.Globalization.Translate(Texts.NoPermissionToUpload)));
                Log.Error(string.Format(Texts.NoPermissionToUpload + " {0}", destinationUrl), result);
                isValid = false;
            }

            if (!isValid)
            {
                result.Result.errorItems = errorItems;
            }

            return isValid;
        }

        /// <summary>
        /// Validates the file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="result">The result.</param>
        /// <returns>True if file is valid.</returns>
        private static bool ValidateFile(HttpPostedFileBase file, SitecoreViewModelResult result)
        {
            var errorItems = new List<ErrorItem>();
            var fileSize = file.ContentLength;
            var isValid = true;

            if (fileSize > Settings.Media.MaxSizeInDatabase)
            {
                errorItems.Add(new ErrorItem("size", fileSize.ToString(), string.Format(ClientHost.Globalization.Translate(Texts.FileSizeError), Settings.Media.MaxSizeInDatabase)));
                Log.Error(string.Format(string.Format(Texts.FileSizeError, Settings.Media.MaxSizeInDatabase)), result);
                isValid = false;
            }

            if (!isValid)
            {
                result.Result.errorItems = errorItems;
            }

            return isValid;
        }

        /// <summary>
        /// Parses the destination URL.
        /// </summary>
        /// <param name="destinationUrl">The destination URL.</param>
        /// <returns>The <see cref="string" />.</returns>
        private string ParseDestinationUrl(string destinationUrl)
        {
            if (!destinationUrl.EndsWith("/"))
            {
                destinationUrl = destinationUrl + "/";
            }

            return destinationUrl;
        }

        /// <summary>
        /// Uploads this instance.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="destinationUrl">The destination URL.</param>
        /// <returns>The result.</returns>
        [NotNull]
        private JsonResult DoUpload(string database, string destinationUrl)
        {
            var name = string.Empty;

            if (string.IsNullOrEmpty(destinationUrl))
            {
                destinationUrl = Sitecore.Constants.MediaLibraryPath;
            }

            var uploadedFileItems = new List<UploadedFileItem>();
            var result = new SitecoreViewModelResult();

            if (!ValidateDestination(database, destinationUrl, result))
            {
                this.Response.StatusCode = new HttpStatusCodeResult(HttpStatusCode.NotFound).StatusCode;
                this.Response.TrySkipIisCustomErrors = true;
                return result;
            }

            var uploadPipelineArgs = new UploadArgs
            {
                Files = System.Web.HttpContext.Current.Request.Files,
                Destination = UploadDestination.Database,
                Folder = this.ParseDestinationUrl(destinationUrl),
                Overwrite = false,
                Unpack = false,
                Versioned = Settings.Media.UploadAsVersionableByDefault,
                Language = Language.Current,
                CloseDialogOnEnd = false
            };

            var file = this.Request.Files[0];
            if (file != null)
            {
                name = Path.GetFileNameWithoutExtension(file.FileName);
                if (!string.IsNullOrEmpty(this.Request.Form["name"]))
                {
                    name = this.Request.Form["name"];
                }

                name = ItemUtil.ProposeValidItemName(name, "default");
            }

            Pipeline pipeline = PipelineFactory.GetPipeline("uiUpload");
            pipeline.Start(uploadPipelineArgs);

            if (!string.IsNullOrEmpty(uploadPipelineArgs.ErrorText))
            {
                var errorResult = new SitecoreViewModelResult();
                errorResult.Result.errorItems = new List<ErrorItem>
        {
          new ErrorItem("uiUpload", string.Empty, Texts.UploadPipelineError),
          new ErrorItem("uiUploadInnerMessage", string.Empty, uploadPipelineArgs.ErrorText)
        };
                return errorResult;
            }


            foreach (var item in uploadPipelineArgs.UploadedItems)
            {

                item.Editing.BeginEdit();
                if (!string.IsNullOrEmpty(name))
                {
                    item.Name = name;
                }
                if (!string.IsNullOrEmpty(this.Request.Form["description"]))
                {
                    item["Description"] = this.Request.Form["description"];
                }
                if (!string.IsNullOrEmpty(this.Request.Form["alternate"]))
                {
                    item["Alt"] = this.Request.Form["alternate"];
                }
                item.Editing.EndEdit();

                Data.Database uploadDatabase = Context.ContentDatabase;
                if (!string.IsNullOrEmpty(database))
                {
                    uploadDatabase = Factory.GetDatabase(database) ?? uploadDatabase;
                }

                var mediaUrlOptions = new MediaUrlOptions(130, 130, false)
                {
                    Thumbnail = true,
                    BackgroundColor = Color.Transparent,
                    Database = uploadDatabase
                };

                var mediaUrl = MediaManager.GetMediaUrl(item, mediaUrlOptions);

                uploadedFileItems.Add(new UploadedFileItem(item.Name, item.ID.ToString(), item.ID.ToShortID().ToString(), mediaUrl));
            }

            result.Result.uploadedFileItems = uploadedFileItems;

            return result;
        }

        /// <summary>
        /// Uploads this instance.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="destinationUrl">The destination URL.</param>
        /// <returns>The result.</returns>
        [NotNull]
        [Obsolete("Use DoUpload with pipeline inside", false)]
        private JsonResult DoUploadSpeak(string database, string destinationUrl)
        {

            if (string.IsNullOrEmpty(destinationUrl))
            {
                destinationUrl = Sitecore.Constants.MediaLibraryPath;
            }

            var uploadedFileItems = new List<UploadedFileItem>();
            var result = new SitecoreViewModelResult();

            if (!ValidateDestination(database, destinationUrl, result))
            {
                return result;
            }

            foreach (string key in this.Request.Files)
            {
                var file = this.Request.Files[key];
                if (file == null)
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(file.FileName);
                if (!string.IsNullOrEmpty(this.Request.Form["name"]))
                {
                    name = this.Request.Form["name"];
                }

                name = ItemUtil.ProposeValidItemName(name, "default");

                var alternateText = string.Empty;
                if (!string.IsNullOrEmpty(this.Request.Form["alternate"]))
                {
                    alternateText = this.Request.Form["alternate"];
                }

                Data.Database uploadDatabase = Context.ContentDatabase;
                if (!string.IsNullOrEmpty(database))
                {
                    uploadDatabase = Factory.GetDatabase(database);
                }

                if (uploadDatabase == null)
                {
                    uploadDatabase = Context.ContentDatabase;
                }

                var options = new MediaCreatorOptions
                {
                    AlternateText = alternateText,
                    Database = uploadDatabase,
                    FileBased = Settings.Media.UploadAsFiles,
                    IncludeExtensionInItemName = Settings.Media.IncludeExtensionsInItemNames,
                    OverwriteExisting = true,
                    Language = LanguageManager.DefaultLanguage,
                    Versioned = Settings.Media.UploadAsVersionableByDefault,
                    Destination = this.ParseDestinationUrl(destinationUrl) + name
                };

                if (!ValidateFile(file, result))
                {
                    return result;
                }

                var item = MediaManager.Creator.CreateFromStream(file.InputStream, "/upload/" + file.FileName, options);

                if (!string.IsNullOrEmpty(this.Request.Form["description"]))
                {
                    item.Editing.BeginEdit();
                    item["Description"] = this.Request.Form["description"];
                    item.Editing.EndEdit();
                }

                var mediaItem = new MediaItem(item);

                var mediaUrlOptions = new MediaUrlOptions(130, 130, false)
                {
                    Thumbnail = true,
                    BackgroundColor = Color.Transparent,
                    Database = mediaItem.Database
                };

                var mediaUrl = MediaManager.GetMediaUrl(mediaItem, mediaUrlOptions);

                uploadedFileItems.Add(new UploadedFileItem(item.Name, item.ID.ToString(), item.ID.ToShortID().ToString(), mediaUrl));
            }

            result.Result.uploadedFileItems = uploadedFileItems;

            return result;
        }

        #endregion
    }
}