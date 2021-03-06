/*
 * Copyright (c) 2016 Samsung Electronics Co., Ltd All Rights Reserved
 *
 * Licensed under the Apache License, Version 2.0 (the License);
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an AS IS BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Contains Interop declarations of OAuth2 classes.
/// </summary>
internal static partial class Interop
{
    /// <summary>
    /// Safehandle wrapper for OAuth2 native handles.
    /// </summary>
    internal abstract class SafeOauth2Handle : SafeHandle
    {
        public SafeOauth2Handle() : base(IntPtr.Zero, true)
        {
        }

        public SafeOauth2Handle(IntPtr handle) : base(handle, true)
        {
        }

        public override bool IsInvalid
        {
            get
            {
                return handle == IntPtr.Zero;
            }
        }

        public abstract void Destroy();

        protected override bool ReleaseHandle()
        {
            Destroy();
            SetHandle(IntPtr.Zero);
            return true;
        }
    }
}
