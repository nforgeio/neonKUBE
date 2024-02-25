// -----------------------------------------------------------------------------
// FILE:	    HelmChart.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Neon.Kube.Helm
{
    /// <summary>
    /// Describes a Helm <b>Chart.yaml</b> file.
    /// </summary>
    public class HelmChart
    {
        //---------------------------------------------------------------------
        // Types

        /// <summary>
        /// Describes a dependant Helm chart.
        /// </summary>
        public class Dependency
        {
            /// <summary>
            /// Specifies the chart name.
            /// </summary>
            [YamlMember(Alias = "name", ApplyNamingConventions = false)]
            public string Name { get; set; }

            /// <summary>
            /// Specifies the chart version.
            /// </summary>
            [YamlMember(Alias = "version", ApplyNamingConventions = false)]
            public string Version { get; set; }

            /// <summary>
            /// Specifies the chart repository.
            /// </summary>
            [YamlMember(Alias = "repository", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string Repository { get; set; }

            /// <summary>
            /// Optionally specifies a yaml path that resolves to a boolean, used
            /// for enabling/disabling charts (e.g. subchart1.enabled )
            /// </summary>
            [YamlMember(Alias = "condition", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string Condition { get; set; }

            /// <summary>
            /// Optionally specifies tags that can can be used to group charts for
            /// enabling/disabling together.
            /// </summary>
            [YamlMember(Alias = "tags", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public List<string> Tags { get; set; }

            /// <summary>
            /// Optionally specifies the mapping of source values to parent key to be
            /// imported. Each item can be a string or pair of child/parent sublist items.
            /// </summary>
            [YamlMember(Alias = "import-values", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public List<string> ImportValues { get; set; }

            /// <summary>
            /// Optionally an alias to be used for the chart. Useful when you have to
            /// add the same chart multiple times.
            /// </summary>
            [YamlMember(Alias = "alias", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string Alias { get; set; }
        }

        /// <summary>
        /// Describes a Helm chart maintainer.
        /// </summary>
        public class Maintainer
        {
            /// <summary>
            /// Specifies the maintainer's name.
            /// </summary>
            [YamlMember(Alias = "name", ApplyNamingConventions = false)]
            public string Name { get; set; }

            /// <summary>
            /// Optionally specifies the maintainer's email address.
            /// </summary>
            [YamlMember(Alias = "email", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string Email { get; set; }

            /// <summary>
            /// Optionally specifies the maintainer's URL.
            /// </summary>
            [YamlMember(Alias = "url", ApplyNamingConventions = false)]
            [DefaultValue(null)]
            public string Url { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Specifies the chart API version.
        /// </summary>
        [YamlMember(Alias = "apiVersion", ApplyNamingConventions = false)]
        public string ApiVersion { get; set; }

        /// <summary>
        /// Specifies the chart name.
        /// </summary>
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the chart version.
        /// </summary>
        [YamlMember(Alias = "version", ApplyNamingConventions = false)]
        public string Version { get; set; }

        /// <summary>
        /// Optionally specifies the semantic version range for compatible Kubernetes versions.
        /// </summary>
        [YamlMember(Alias = "kubeVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeVersion { get; set; }

        /// <summary>
        /// Optionallyt describes the chart.
        /// </summary>
        [YamlMember(Alias = "description", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Description { get; set; }

        /// <summary>
        /// Optionally specifies the chart type.
        /// </summary>
        [YamlMember(Alias = "type", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Type { get; set; }

        /// <summary>
        /// Optionally specifies project keywords.
        /// </summary>
        [YamlMember(Alias = "keywords", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Keywords { get; set; }

        /// <summary>
        /// Optionally specifies the URL of this project's home page.
        /// </summary>
        [YamlMember(Alias = "home", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Home { get; set; }

        /// <summary>
        /// Optionally specifies the URLs to the source code for tbhis project.
        /// </summary>
        [YamlMember(Alias = "sources", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Sources { get; set; }

        /// <summary>
        /// Optionally specifies any dependent charts.
        /// </summary>
        [YamlMember(Alias = "dependencies", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Dependency> Dependencies { get; set; }

        /// <summary>
        /// Optionally describes the chart maintainers.
        /// </summary>
        [YamlMember(Alias = "maintainers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Maintainer> Maintainers { get; set; }

        /// <summary>
        /// Optionally specifies a URL to an SVG or PNG image to be used as an icon.
        /// </summary>
        [YamlMember(Alias = "icon", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Icon { get; set; }

        /// <summary>
        /// Optionally specifiies the version of the application this chart deploys.
        /// </summary>
        [YamlMember(Alias = "appVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AppVersion { get; set; }

        /// <summary>
        /// Optionally specifies that this chart is deprecated.
        /// </summary>
        [YamlMember(Alias = "deprecated", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Deprecated { get; set; }

        /// <summary>
        /// Optionally specifies chart annotations.
        /// </summary>
        [YamlMember(Alias = "annotations", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> Annotations { get; set; }
    }
}
