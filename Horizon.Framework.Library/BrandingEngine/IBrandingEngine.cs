﻿namespace Horizon.Framework
{
    public interface IBrandingEngine
    {
        /// <summary>
        /// The name of the product, as displayed to the user.
        /// </summary>
        string ProductName { get; }

        /// <summary>
        /// The name of the storage folder to use when storing information
        /// inside the Application Data (or Linux / Mac equivalent).
        /// </summary>
        string ProductStorageID { get; }

#if PLATFORM_WINDOWS
        System.Drawing.Icon WindowsIcon { get; }
#elif PLATFORM_LINUX
        Gdk.Pixbuf LinuxIcon { get; }
#endif
    }
}