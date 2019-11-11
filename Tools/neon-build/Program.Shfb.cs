//-----------------------------------------------------------------------------
// FILE:	    Program.Shfb.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        private class ContentFile
        {
            public ContentFile(string topicId)
            {
                this.TopicId = topicId;
            }

            /// <summary>
            /// The SHFB generated topic ID.
            /// </summary>
            public string TopicId { get; private set; }

            /// <summary>
            /// Path to the content [*.aml] file in the source repo.
            /// </summary>
            public string ContentPath { get; set; }

            /// <summary>
            /// HTML file name to be generated for this for this topic.
            /// </summary>
            public string FileName { get; set; }
        }

        /// <summary>
        /// Handles post-processing of a SHFB generated documentation site.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void Shfb(CommandLine commandLine)
        {
            // $hack(jefflill): This is somewhat fragile because it depends on how SHFB generates HTML pages.

            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length != 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var shfbFolder = commandLine.Arguments[0];
            var outputFolder = commandLine.Arguments[1];
            var gtagPath = commandLine.GetOption("--gtag");
            var dryRun = commandLine.HasOption("--dryrun");
            var gtag = !string.IsNullOrEmpty(gtagPath) ? File.ReadAllText(gtagPath) : null;
            var html = string.Empty;

            // Verify that the [gtag.js] file looks reasonable.

            if (gtag != null && (!gtag.Contains("Google Analytics") || !gtag.Contains("<script")))
            {
                Console.Error.WriteLine($"[{gtagPath}] does not look like a valid Google Analytics [gtag.js] file.");
                Program.Exit(1);
            }

            // Read the [ContentLayout.content] file in the help project and then all of the
            // [*.aml] files within the [./Content] folder to create a map of the SHFB generated 
            // topic IDs to any custom topic IDs embedded in the content.
            //
            // Note that I could be parsing these files via an XML parser, but I'm going to be
            // lazy and just hack the parsing myself.  This is fragile again, but the format
            // of these files is very unlikely to change and this is an internal tool.

            var topicIdToContent = new Dictionary<string, ContentFile>();

            foreach (var rawLine in File.ReadAllLines(Path.Combine(shfbFolder, "ContentLayout.content")))
            {
                var line = rawLine.Trim();

                if (line.StartsWith("<Topic "))
                {
                    var startPos = line.IndexOf("id=\"");

                    if (startPos == -1)
                    {
                        continue;
                    }

                    startPos += "id=\"".Length;

                    var endPos = line.IndexOf('"', startPos);

                    if (endPos == -1)
                    {
                        continue;
                    }

                    var topicId = line.Substring(startPos, endPos - startPos);

                    topicIdToContent.Add(topicId, new ContentFile(topicId));
                }
            }

            foreach (var contentPath in Directory.EnumerateFiles(Path.Combine(shfbFolder, "Content"), "*.aml", SearchOption.AllDirectories))
            {
                string topicId = null;
                string topicFilename = null;

                foreach (var rawLine in File.ReadAllLines(contentPath))
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("<!-- topic-filename=\""))
                    {
                        var startPos = "<!-- topic-filename=\"".Length;
                        var endPos = line.IndexOf('"', startPos);

                        if (endPos == -1)
                        {
                            continue;   // Malformed topic file name?
                        }

                        topicFilename = line.Substring(startPos, endPos - startPos).Trim();

                        if (string.IsNullOrWhiteSpace(topicFilename))
                        {
                            continue;   // Malformed topic file name?
                        }
                    }
                    else if (line.StartsWith("<topic id=\""))
                    {
                        var startPos = "<topic id=\"".Length;
                        var endPos = line.IndexOf('"', startPos);

                        if (endPos == -1)
                        {
                            continue;   // Malformed <topic.../> element?
                        }

                        topicId = line.Substring(startPos, endPos - startPos);
                    }
                }

                if (topicId != null && topicIdToContent.TryGetValue(topicId, out var contentFile))
                {
                    contentFile.ContentPath = contentPath;
                    contentFile.FileName = (topicFilename ?? contentFile.TopicId) + ".htm";
                }
            }

            // Ensure that all of the content file names are unique because they're
            // all going to be hosted in the same website folder.

            var contentFileNames = new HashSet<string>();
            var contentError = false;

            foreach (var topic in topicIdToContent)
            {
                if (contentFileNames.Contains(topic.Value.FileName))
                {
                    Console.Error.WriteLine($"ERROR: Content Topic [id={topic.Key}] generates as [file={topic.Value.FileName}] which conflicts with another topic.");
                    contentError = true;
                }
            }

            if (contentError)
            {
                Program.Exit(1);
            }

            // Exit if we're only doing a dry run.

            if (dryRun)
            {
                Program.Exit(0);
                return;
            }

            // Munge the help files.

            Console.WriteLine($"neon-build gtag \"{gtagPath}\" \"{shfbFolder}\"");
            Console.WriteLine("Processing special pages...");

            // Special-case the [*.html] files (I believe there are only two of them).

            foreach (var pagePath in Directory.EnumerateFiles(shfbFolder, "*.html", SearchOption.AllDirectories))
            {
                html = File.ReadAllText(pagePath);

                // Insert the [gtag.js] if requested.

                if (gtag != null)
                {
                    if (html.Contains(gtag))
                    {
                        // The page already looks like it's already been modified.

                        continue;
                    }

                    html = html.Replace("<head>\r\n", "<head>\r\n" + gtag);
                }

                File.WriteAllText(pagePath, html);
            }

            // Replace the [index.html] file with the contents of the HTML page it
            // references and then delete the referenced file.  We're also going to
            // track the index topic ID so any references to it will be updated to
            // just [index.html].

            var indexPath = Path.Combine(outputFolder, "index.html");

            html = File.ReadAllText(indexPath);

            var indexTopicIdPath = Path.Combine(outputFolder, "index.topicid");
            var pRefStart        = html.IndexOf("<a href=\"html/");
            var indexTopicId     = (string)null;
            int pRefEnd;

            if (pRefStart != -1)
            {
                pRefStart += "<a href=\"html/".Length;
                pRefEnd = html.IndexOf(".htm\"", pRefStart + "<a href=\"html/".Length);

                if (pRefEnd != -1)
                {
                    indexTopicId = html.Substring(pRefStart, pRefEnd - pRefStart);

                    var indexRefPath = Path.Combine(outputFolder, "html", indexTopicId + ".htm");
                    var contents = File.ReadAllText(indexRefPath);

                    File.WriteAllText(indexPath, contents);
                    File.Delete(indexRefPath);

                    // Write a [index.topicid] file with the topic ID so running the tool
                    // again will be able to identify this.

                    File.WriteAllText(indexTopicIdPath, indexTopicId);

                    // We also need to munge the [search.html] file so the back button goes
                    // to [index.html].

                    var searchPath = Path.Combine(outputFolder, "search.html");

                    html = File.ReadAllText(searchPath);
                    html = html.Replace("html/" + indexTopicId + ".htm", "index.html");

                    File.WriteAllText(searchPath, html);
                }
            }
            else
            {
                indexTopicId = File.ReadAllText(indexTopicIdPath);
            }

            // Update the index topic so it references [index.html]

            topicIdToContent[indexTopicId].FileName = "index.html";

            // Now process the [*.htm/html] files that hold all of the site content:
            //
            //  1. Insert the [gtag.js] scripts when requested
            //  2. Replace GUID based conceptual topic references with friendly names

            Console.WriteLine("Processing content pages...");

            var pagePaths = Directory.EnumerateFiles(outputFolder, "*.htm", SearchOption.AllDirectories)
                .Union(Directory.EnumerateFiles(outputFolder, "*.html", SearchOption.AllDirectories))
                .ToArray();

            var count  = 0;
            var sbPage = new StringBuilder();

            foreach (var rawPagePath in pagePaths)
            {
                var pagePath = rawPagePath;

                if (count % 500 == 0)
                {
                    var pageCount = (count + 1).ToString();
                    var padding   = new string(' ', pagePaths.Length.ToString().Length - pageCount.Length);

                    Console.WriteLine($"Processing page: {padding}{pageCount}/{pagePaths.Length}");
                }

                count++;

                // Rename any file whose name is a GUID topic ID and that has a friendly name
                // mapping to to use the friendly name.

                if (topicIdToContent.TryGetValue(Path.GetFileName(pagePath).Replace(".htm", string.Empty), out var contentFile))
                {
                    var newPagePath = Path.Combine(Path.GetDirectoryName(pagePath), contentFile.FileName);

                    File.Move(pagePath, newPagePath);
                    pagePath = newPagePath;
                }

                html = File.ReadAllText(pagePath);

                // Insert the [gtag.js] if requested and it is not already present..

                if (gtag != null && !html.Contains(gtag))
                {
                    html = html.Replace("<html><head>", "<html><head>" + gtag);
                }

                // We also need to remove [href] and [src] references
                // to any of the local directories.

                html = html.Replace("src=\"../fti/", "src=\"fti/");
                html = html.Replace("src=\"../icons/", "src=\"icons/");
                html = html.Replace("src=\"../media/", "src=\"media/");
                html = html.Replace("src=\"../scripts/", "src=\"scripts/");
                html = html.Replace("src=\"../styles/", "src=\"styles/");
                html = html.Replace("src=\"../toc/", "src=\"toc/");

                html = html.Replace("href=\"../fti/", "href=\"fti/");
                html = html.Replace("href=\"../html/", "href=\"");
                html = html.Replace("href=\"../icons/", "href=\"icons/");
                html = html.Replace("href=\"../media/", "href=\"scripts/");
                html = html.Replace("href=\"../scripts/", "href=\"scripts/");
                html = html.Replace("href=\"../styles/", "href=\"styles/");
                html = html.Replace("href=\"../toc/", "href=\"toc/");

                // Replace any references to topic IDs with their friendly names.

                foreach (var content in topicIdToContent.Values)
                {
                    html = html.Replace($"content=\"{content.TopicId}", $"content=\"{Path.GetFileNameWithoutExtension(content.FileName)}");
                    html = html.Replace($"href=\"{content.TopicId}.htm", $"href=\"{content.FileName}");
                }

                // Update the file.

                File.WriteAllText(pagePath, html);
            }

            // Relocate any files in the [html/*] folder to the parent folder and
            // then delete the [html] folder.

            Console.WriteLine("Relocating HTML files...");

            var htmlFolder = Path.Combine(outputFolder, "html");

            if (Directory.Exists(htmlFolder))
            {
                foreach (var file in Directory.GetFiles(htmlFolder, "*.*", SearchOption.TopDirectoryOnly).ToList())
                {
                    File.Move(file, Path.Combine(outputFolder, Path.GetFileName(file)));
                }

                NeonHelper.DeleteFolder(htmlFolder);
                Directory.Delete(htmlFolder);
            }

            Console.WriteLine("Munging TOC files...");

            // Munge the [WebKI.xml], [WebTOC.xml], and [toc/*.xml] files by stripping any [html/] 
            // prefixes from  the Url attributes.  We're also going to replace any GUID based references
            // that map friendly topics to the friendly IDs.

            var tocFilePaths = Directory.GetFiles(Path.Combine(outputFolder, "toc")).ToList();

            tocFilePaths.Add(Path.Combine(outputFolder, "WebKI.xml"));
            tocFilePaths.Add(Path.Combine(outputFolder, "WebTOC.xml"));

            foreach (var filePath in tocFilePaths)
            {
                var xml = File.ReadAllText(filePath);

                // Remove any HTML folder prefixes.

                xml = xml.Replace("Url=\"html/", "Url=\"");

                // Make any GUID --> friendly name replacements.

                var sbXml    = new StringBuilder();
                int lastPos  = 0;
                int startPos = 0;
                int endPos;

                while (true)
                {
                    startPos = xml.IndexOf("Url=\"", lastPos);

                    if (startPos == -1)
                    {
                        sbXml.Append(xml.Substring(lastPos));
                        break;
                    }

                    startPos += "Url=\"".Length;

                    sbXml.Append(xml.Substring(lastPos, startPos - lastPos));

                    endPos = xml.IndexOf(".htm\"", startPos);

                    if (endPos == -1)
                    {
                        sbXml.Append(xml.Substring(startPos));
                        break;
                    }

                    var topicId = xml.Substring(startPos, endPos - startPos);

                    if (topicIdToContent.TryGetValue(topicId, out var contentFile))
                    {
                        sbXml.Append(contentFile.FileName);
                    }
                    else
                    {
                        sbXml.Append(topicId);
                    }

                    lastPos = endPos;
                }

                File.WriteAllText(filePath, sbXml.ToString());
            }

            Program.Exit(0);
        }
    }
}
