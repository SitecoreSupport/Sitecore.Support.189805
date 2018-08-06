namespace Sitecore.Support.XA.Foundation.Editing.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.DependencyInjection;
    using Sitecore.Diagnostics;
    using Sitecore.Exceptions;
    using Sitecore.Globalization;
    using Sitecore.Shell.Applications.ContentEditor;
    using Sitecore.Shell.Applications.Dialogs.MediaBrowser;
    using Sitecore.Shell.Applications.WebEdit.Commands;
    using Sitecore.Shell.Framework.Commands;
    using Sitecore.Web;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.XA.Foundation.Abstractions;
    using System;
    using System.Linq;

    [Serializable]
    public class ChooseImage : Sitecore.XA.Foundation.Editing.Commands.ChooseImage
    {
        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            WebEditCommand.ExplodeParameters(context);
            string formValue = WebUtil.GetFormValue("scPlainValue");
            context.Parameters.Add("fieldValue", formValue);
            if (context.Items.Any())
            {
                context.Parameters.Add("contextItem", context.Items.First().ID.ToString());
            }
            ServiceLocator.ServiceProvider.GetService<IContext>().ClientPage.Start(this, "Run", context.Parameters);
        }

        protected override void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            Item itemNotNull = Client.GetItemNotNull(args.Parameters["itemid"], Language.Parse(args.Parameters["language"]));
            itemNotNull.Fields.ReadAll();
            Field field = itemNotNull.Fields[args.Parameters["fieldid"]];
            Assert.IsNotNull(field, "field");
            ImageField imageField = new ImageField(field, field.Value);
            string str = args.Parameters["controlid"];
            if (args.IsPostBack)
            {
                if (args.Result != "undefined")
                {
                    string value;
                    if (!string.IsNullOrEmpty(args.Result))
                    {
                        MediaItem mediaItem = Client.ContentDatabase.Items[args.Result];
                        if (mediaItem != null)
                        {
                            imageField.SetAttribute("mediaid", mediaItem.ID.ToString());
                            if (!string.IsNullOrEmpty(args.Parameters["fieldValue"]))
                            {
                                XmlValue xmlValue = new XmlValue(args.Parameters["fieldValue"], "image");
                                string attribute = xmlValue.GetAttribute("height");
                                if (!string.IsNullOrEmpty(attribute))
                                {
                                    imageField.Height = attribute;
                                }
                                string attribute2 = xmlValue.GetAttribute("width");
                                if (!string.IsNullOrEmpty(attribute2))
                                {
                                    imageField.Width = attribute2;
                                }
                            }
                        }
                        else
                        {
                            SheerResponse.Alert("Item not found.", Array.Empty<string>());
                        }
                        value = imageField.Value;
                    }
                    else
                    {
                        value = string.Empty;
                    }
                    string value2 = WebEditImageCommand.RenderImage(args, value);
                    SheerResponse.SetAttribute("scHtmlValue", "value", value2);
                    SheerResponse.SetAttribute("scPlainValue", "value", value);
                    SheerResponse.Eval("scSetHtmlValue('" + str + "')");
                }
            }
            else
            {
                string text = StringUtil.GetString(ResolveSourceQuery(field.Source, itemNotNull), "/sitecore/media library");
                string text2 = imageField.GetAttribute("mediaid");
                if (text.StartsWith("~", StringComparison.InvariantCulture))
                {
                    if (string.IsNullOrEmpty(text2))
                    {
                        text2 = StringUtil.Mid(text, 1);
                    }
                    text = "/sitecore/media library";
                }
                Language language = itemNotNull.Language;
                MediaBrowserOptions mediaBrowserOptions = new MediaBrowserOptions();
                Item item = Client.ContentDatabase.GetItem(text, language);
                if (item == null)
                {
                    throw new ClientAlertException("The source of this Image field points to an item that does not exist.");
                }
                mediaBrowserOptions.Root = item;
                mediaBrowserOptions.AllowEmpty = true;
                #region Added code
                mediaBrowserOptions.SelectedItem = itemNotNull;
                #endregion
                SelectSiteMediaRoot(args, mediaBrowserOptions);
                if (!string.IsNullOrEmpty(text2))
                {
                    Item item2 = Client.ContentDatabase.GetItem(text2, language);
                    if (item2 != null)
                    {
                        mediaBrowserOptions.SelectedItem = item2;
                    }
                }
                SheerResponse.ShowModalDialog(mediaBrowserOptions.ToUrlString().ToString(), "1200px", "700px", string.Empty, true);
                args.WaitForPostBack();
            }
        }
    }
}