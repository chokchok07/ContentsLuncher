using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace ShowroomPowerController
{
    public static class FontHelper
    {
        private static PrivateFontCollection privateFontCollection = new PrivateFontCollection();
        private static FontFamily pretendardFamily;
        private static bool isLoaded = false;

        public static FontFamily PretendardFamily
        {
            get
            {
                if (!isLoaded)
                {
                    LoadFont();
                }
                return pretendardFamily;
            }
        }

        private static void LoadFont()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = null;
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("PretendardVariable.ttf", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName != null)
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            byte[] fontData = new byte[stream.Length];
                            stream.Read(fontData, 0, (int)stream.Length);
                            
                            // Write to a stable temp file to avoid WinForms memory font bugs
                            string tempPath = Path.Combine(Path.GetTempPath(), "Pretendard-Medium-Temp.otf");
                            try
                            {
                                File.WriteAllBytes(tempPath, fontData);
                                privateFontCollection.AddFontFile(tempPath);
                                pretendardFamily = privateFontCollection.Families[0];
                                isLoaded = true;
                            }
                            catch
                            {
                                // Fallback to memory font loading
                                IntPtr fontPtr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(fontData.Length);
                                System.Runtime.InteropServices.Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
                                privateFontCollection.AddMemoryFont(fontPtr, fontData.Length);
                                pretendardFamily = privateFontCollection.Families[0];
                                isLoaded = true;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback
            }

            if (!isLoaded)
            {
                pretendardFamily = new FontFamily("Malgun Gothic");
            }
        }

        public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(PretendardFamily, size, style);
        }
    }
}
