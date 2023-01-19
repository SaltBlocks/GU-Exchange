using ImageProcessor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Threading;

namespace GU_Exchange
{
    /// <summary>
    /// A dictionary of a fixed size, used to maintain a buffer of previously searched card images.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class SizedDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {

        private int maxSize;
        private Queue<TKey> keys;

        public SizedDictionary(int size)
        {
            maxSize = size;
            keys = new Queue<TKey>();
        }

        new public void Add(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException();
            base.Add(key, value);
            keys.Enqueue(key);
            if (keys.Count > maxSize) base.Remove(keys.Dequeue());
        }

        new public bool Remove(TKey key)
        {
            if (key == null) throw new ArgumentNullException();
            if (!keys.Contains(key)) return false;
            var newQueue = new Queue<TKey>();
            while (keys.Count > 0)
            {
                var thisKey = keys.Dequeue();
                if (!thisKey.Equals(key)) newQueue.Enqueue(thisKey);
            }
            keys = newQueue;
            return base.Remove(key);
        }
    }

    /// <summary>
    /// Used to fetch images from either the disk of from the network to display in the App.
    /// </summary>
    internal class ResourceFactory
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr value);

        /// <summary>
        /// Converts an <see cref="System.Drawing.Image" /> to a <see cref="System.Windows.Media.Imaging.BitmapSource" />.
        /// </summary>
        /// <param name="myImage">The <see cref="System.Drawing.Image" /> to convert.</param>
        /// <returns> The provided image as a <see cref="System.Windows.Media.Imaging.BitmapSource" /></returns>
        private static BitmapSource GetImageStream(System.Drawing.Image myImage)
        {
            var bitmap = new Bitmap(myImage);
            IntPtr bmpPt = bitmap.GetHbitmap();
            BitmapSource bitmapSource =
             System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                   bmpPt,
                   IntPtr.Zero,
                   Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());

            //freeze bitmapSource and clear memory to avoid memory leaks
            bitmapSource.Freeze();
            DeleteObject(bmpPt);

            return bitmapSource;
        }

        /// <summary>
        /// Holds the last 100 requested images for quick loading.
        /// </summary>
        public static SizedDictionary<int, BitmapSource> imgCache = new(100);

        /// <summary>
        /// Used to track files currently opened by a Thread on this application.
        /// </summary>
        private static HashSet<string> filesInUse = new();

        /// <summary>
        /// Waits until no threads indicate they are currently accessing the provided file link.
        /// Reserves the provided file link for access by the calling function.
        /// </summary>
        /// <param name="fileLink">The file to reserve.</param>
        private static void awaitFile(string fileLink)
        {
            while (filesInUse.Contains(fileLink))
            {
                Thread.Sleep(10);
            }
            lock (filesInUse)
            {
                filesInUse.Add(fileLink);
            }
            return;
        }

        /// <summary>
        /// Waits asyncronously until no threads indicate they are currently accessing the provided file link.
        /// Reserves the provided file link for access by the calling function.
        /// </summary>
        /// <param name="fileLink">The file to reserve.</param>
        private static async Task awaitFileAsync(string fileLink)
        {
            while (filesInUse.Contains(fileLink))
            {
                await Task.Delay(10);
            }
            lock (filesInUse)
            {
                filesInUse.Add(fileLink);
            }
            return;
        }

        /// <summary>
        /// Indicate that the calling function is done accessing the file at the provided link.
        /// This file can then be reserved by a different thread.
        /// </summary>
        /// <param name="fileLink">The file that was reserved.</param>
        private static void freeFile(string fileLink)
        {
            lock (filesInUse)
            {
                filesInUse.Remove(fileLink);
            }
        }

        /// <summary>
        /// Loads the image of the plain card with the provided ID from the disk.
        /// </summary>
        /// <param name="CardID">The ID of the card to load.</param>
        /// <returns>The image of the card as a <see cref="System.Windows.Media.Imaging.BitmapSource" /> or null if it doesn't exist on the disk.</returns>
        public static async Task<BitmapSource?> GetImageFromDiskAsync(int CardID)
        {
            Task<BitmapSource?> imgGet = Task.Run(() =>
            {
                if (!File.Exists("cards/" + CardID + "q5.webp"))
                {
                    return null;
                }
                awaitFile("cards/" + CardID + "q5.webp");
                byte[] imgBytes = File.ReadAllBytes("cards/" + CardID + "q5.webp");
                freeFile("cards/" + CardID + "q5.webp");
                using (ImageFactory fact = new())
                {
                    using (MemoryStream imgStream = new MemoryStream(imgBytes))
                    {
                        fact.Load(imgStream);
                        BitmapSource res = GetImageStream(fact.Image);
                        return res;
                    }
                }
            });
            return await imgGet;
        }

        /// <summary>
        /// Fetches the image of the plain card with the provided ID from the godsunchained website.
        /// After it is loaded, the image is stored on the disk.
        /// </summary>
        /// <param name="CardID">The ID of the card to load.</param>
        /// <param name="cancelToken">Token used to cancel the task.</param>
        /// <returns>The image of the card as a <see cref="System.Windows.Media.Imaging.BitmapSource" />.</returns>
        public static async Task<BitmapSource?> FetchCardImageAsync(int CardID, CancellationToken cancelToken)
        {
            Task<BitmapSource> imgGet = Task.Run(async () =>
            {
                Console.WriteLine($"Downloading {CardID}");
                using (HttpClient client = new())
                {
                    Stream imgStream;
                    while (true)
                    {
                        try
                        {
                            imgStream = await client.GetStreamAsync("https://card.godsunchained.com/?id=" + CardID + "&q=5");
                            break;
                        }
                        catch (HttpRequestException)
                        {
                            await Task.Delay(1000, cancelToken);
                        }
                    }

                    byte[] imgData;
                    using (var memoryStream = new MemoryStream())
                    {
                        imgStream.CopyTo(memoryStream);
                        imgData = memoryStream.ToArray();
                    }

                    await awaitFileAsync("cards/" + CardID + "q5.webp");
                    while (true)
                    {
                        try
                        {
                            using (var fileStream = File.Create("cards/" + CardID + "q5.webp"))
                            {
                                using (MemoryStream stream = new MemoryStream(imgData))
                                {
                                    await stream.CopyToAsync(fileStream);
                                }
                            }
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(1000, cancelToken);
                        }
                    }
                    freeFile("cards/" + CardID + "q5.webp");
                    using (ImageFactory fact = new())
                    {
                        using (MemoryStream stream = new MemoryStream(imgData))
                        {
                            fact.Load(stream);
                            BitmapSource res = GetImageStream(fact.Image);
                            return res;
                        }
                    }
                }
            });
            return await imgGet;
        }

        /// <summary>
        /// Loads the image for the card with the provided card ID.
        /// Will load the image from disk if it exists. Otherwise, fetches it from the gods unchained website.
        /// </summary>
        /// <param name="CardID">The ID for the card to load.</param>
        /// <param name="cancelToken">Token used to cancel the task.</param>
        /// <returns>The image of the card as a <see cref="System.Windows.Media.Imaging.BitmapSource" />.</returns>
        public static async Task<BitmapSource?> GetCardImageAsync(int CardID, CancellationToken cancelToken)
        {
            if (imgCache.ContainsKey(CardID))
            {
                return imgCache[CardID];
            }
            BitmapSource? res;
            if (!File.Exists("cards/" + CardID + "q5.webp"))
            {
                res = await FetchCardImageAsync(CardID, cancelToken);
            }
            else
            {
                res = await GetImageFromDiskAsync(CardID);
            }
            if (res != null && !imgCache.ContainsKey(CardID))
            {
                imgCache.Add(CardID, res);
            }
            return res;
        }
    }
}
