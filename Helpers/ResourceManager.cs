using ImageProcessor;
using ImageProcessor.Common.Exceptions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GU_Exchange.Helpers
{
    class ResourceManager
    {
        public static RateLimitHandler RateLimiter = new RateLimitHandler(new HttpClientHandler(), 5);
        public static HttpClient Client = new(handler: RateLimiter);
        public static SizedDictionary<string, BitmapSource> imgCache = new(30);
        private static string _cardFolder = Path.Combine(Settings.GetConfigFolder(), "cards");

        #region Supporting Methods.
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr value);

        /// <summary>
        /// Converts an <see cref="Image" /> to a <see cref="BitmapSource" />.
        /// </summary>
        /// <param name="myImage">The <see cref="Image" /> to convert.</param>
        /// <returns> The provided image as a <see cref="BitmapSource" /></returns>
        private static BitmapSource GetImageStream(Image myImage)
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
        #endregion

        #region Get card images.
        /// <summary>
        /// Fetches the image of the plain card with the provided ID from the godsunchained website.
        /// After it is loaded, the image is stored on the disk.
        /// </summary>
        /// <param name="CardID">The ID of the card to load.</param>
        /// <param name="cancelToken">Token used to cancel the task.</param>
        /// <returns>The image of the card as a <see cref="BitmapSource" />.</returns>
        public static async Task<BitmapSource?> FetchCardImageAsync(int CardID, int quality, bool save, CancellationToken cancelToken)
        {
            Task<BitmapSource?> imgGet = Task.Run(async () =>
            {
                Stream imgStream;
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, "https://card.godsunchained.com/?id=" + CardID + "&q=" + quality))
                    {
                        request.Headers.Add("Connection", "keep-alive");
                        request.Headers.Add("sec-ch-ua", "\"Microsoft Edge\";v=\"111\", \"Not(A:Brand\";v=\"8\", \"Chromium\";v=\"111\"");
                        request.Headers.Add("sec-ch-ua-mobile", "?0");
                        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36 Edg/111.0.1661.62");
                        request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
                        request.Headers.Add("Accept", "image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                        request.Headers.Add("Sec-Fetch-Site", "cross-site");
                        request.Headers.Add("Sec-Fetch-Mode", "no-cors");
                        request.Headers.Add("Sec-Fetch-Dest", "image");
                        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                        HttpResponseMessage message = await Client.SendAsync(request, cancelToken);
                        imgStream = message.Content.ReadAsStream();
                    }
                }
                catch (HttpRequestException e)
                {
                    Log.Warning($"Failed to fetch card image with proto '{CardID}' of quality {quality}. {e.Message} {e.StackTrace}");
                    return null;
                }

                byte[] imgData;
                using (var memoryStream = new MemoryStream())
                {
                    imgStream.CopyTo(memoryStream);
                    imgData = memoryStream.ToArray();
                }

                BitmapSource res;
                try
                {
                    using (ImageFactory fact = new())
                    {
                        using (MemoryStream stream = new MemoryStream(imgData))
                        {
                            fact.Load(stream);
                            res = GetImageStream(fact.Image);
                        }
                    }
                }
                catch (ImageFormatException)
                {
                    return null;
                }

                if (save)
                {
                    if (!Directory.Exists(_cardFolder))
                    {
                        Directory.CreateDirectory(_cardFolder);
                    }
                    try
                    {
                        using (var fileStream = File.Create(Path.Combine(_cardFolder, CardID + "q" + quality + ".webp")))
                        {
                            using (MemoryStream stream = new MemoryStream(imgData))
                            {
                                await stream.CopyToAsync(fileStream);
                            }
                        }
                    }
                    catch (IOException)
                    {
                        return null;
                    }
                }
                return res;
            });
            return await imgGet;
        }

        /// <summary>
        /// Loads the image of the plain card with the provided ID from the disk.
        /// </summary>
        /// <param name="CardID">The ID of the card to load.</param>
        /// <returns>The image of the card as a <see cref="BitmapSource" /> or null if it doesn't exist on the disk.</returns>
        public static async Task<BitmapSource?> GetImageFromDiskAsync(int CardID, int quality)
        {
            Task<BitmapSource?> imgGet = Task.Run(() =>
            {
                string cardPath = Path.Combine(_cardFolder, CardID + "q" + quality + ".webp");
                if (!File.Exists(cardPath))
                {
                    return null;
                }
                byte[] imgBytes = File.ReadAllBytes(cardPath);
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
        /// Loads the image for the card with the provided card ID.
        /// Will load the image from disk if it exists. Otherwise, fetches it from the gods unchained website.
        /// </summary>
        /// <param name="CardID">The ID for the card to load.</param>
        /// <param name="cancelToken">Token used to cancel the task.</param>
        /// <returns>The image of the card as a <see cref="BitmapSource" />.</returns>
        public static async Task<BitmapSource?> GetCardImageAsync(int CardID, int quality, bool save, CancellationToken cancelToken)
        {
            string cacheKey = CardID.ToString() + '_' + quality.ToString();
            if (imgCache.ContainsKey(cacheKey))
                return imgCache[cacheKey];
            string cardPath = Path.Combine(_cardFolder, CardID + "q" + quality + ".webp");
            BitmapSource? res;
            if (!File.Exists(cardPath))
            {
                res = await FetchCardImageAsync(CardID, quality, save, cancelToken);
            }
            else
            {
                res = await GetImageFromDiskAsync(CardID, quality);
            }
            if (res != null && !imgCache.ContainsKey(cacheKey))
            {
                imgCache.Add(cacheKey, res);
            }
            return res;
        }
        #endregion
    }

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
}
