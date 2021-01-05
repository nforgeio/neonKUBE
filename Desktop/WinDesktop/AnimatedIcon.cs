//-----------------------------------------------------------------------------
// FILE:	    AnimatedIcon.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinDesktop
{
    /// <summary>
    /// Holds a collection of <see cref="Icon"/> instances to be played
    /// in sequence.
    /// </summary>
    public class AnimatedIcon
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads individual frames to be displayed in an icon animation.  These
        /// are expected to be files named like <b>PREFIX-#.ico</b> in the specified
        /// directory.  The method will start loading <b>PREFIX-0.ico</b> and keep
        /// incrementing the index until no more frame files are found.
        /// </summary>
        /// <param name="folder">The source folder path.</param>
        /// <param name="prefix">The icon file name prefix.</param>
        /// <param name="frameRate">The desired frame rate.</param>
        /// <returns>The <see cref="AnimatedIcon"/>.</returns>
        /// <exception cref="FileNotFoundException">Thrown if no matching icon files were found.</exception>
        public static AnimatedIcon Load(string folder, string prefix, double frameRate)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(folder), nameof(folder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(prefix), nameof(prefix));
            Covenant.Requires<ArgumentException>(frameRate > 0, nameof(frameRate));

            var animatedIcon = new AnimatedIcon() { FrameRate = frameRate };

            for (var i = 0; ; i++)
            {
                var path = Path.Combine(folder, $"{prefix}-{i}.ico");

                if (!File.Exists(path))
                {
                    break;
                }

                animatedIcon.frames.Add(new Icon(path));
            }

            if (animatedIcon.frames.Count == 0)
            {
                throw new FileNotFoundException($"Could not locate any icon files with prefix: {prefix}");
            }

            return animatedIcon;
        }

        //---------------------------------------------------------------------
        // Instance members

        private List<Icon>  frames;
        private int         position;

        /// <summary>
        /// Private constructor.
        /// </summary>
        private AnimatedIcon()
        {
            frames   = new List<Icon>();
            position = 0;
        }

        /// <summary>
        /// Returns the animated frame rate (frames/sec).
        /// </summary>
        public double FrameRate { get; private set; }

        /// <summary>
        /// Returns the next frame in the animation.
        /// </summary>
        public Icon GetNextFrame()
        {
            var frame = frames[position++];

            if (position >= frames.Count)
            {
                position = 0;
            }

            return frame;
        }
    }
}
