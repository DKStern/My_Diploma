using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.VectorTypes;
using ManagedCuda.CudaFFT;
using Diploma.Classes;
using Diploma.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace Diploma
{
    public partial class MainWindow : Window
    {
        private ColorData colorData;
        private Color StartColor, FinishColor;
        private int fileCount = 0; //Count of files
        private short[] fileBuffer;
        private string path, tdmsPath; //Path to the directory with data
        private WriteableBitmap bitmap;
        private double maxFreq = 10000.0;
        private int step = 1, startPosition = 0, lengthDepth = 0;
        const int bufSize = 4096;
        private Logger logger, writeLogger;
        private long dataOffset;
        private bool isRequested = true, isReady = false;
        private string logPath;
        private long nfft;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            imageBox.Source = bitmap;
            logPath = $"Logs {DateTime.Now.Day}_{DateTime.Now.Month}_{DateTime.Now.Year} {DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}";
            Directory.CreateDirectory(logPath);
            colorData = new ColorData();
            logger = new Logger($"{logPath}\\Main Log");
            writeLogger = new Logger($"{logPath}\\Writer Log");
            logger.Add($"Начало работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add("");
            StartColor = Color.FromArgb(255, 95, 255, 197);
            FinishColor = Color.FromArgb(255, 133, 8, 8);
            startColorPiker.SelectedColor = GetColor(95, 255, 197);
            finishColorPiker.SelectedColor = GetColor(133, 8, 8);
        }

        /// <summary>
        /// Создаёт цвет из RGB
        /// </summary>
        /// <param name="r">Значение красного цвета</param>
        /// <param name="g">Значение зелёного цвета</param>
        /// <param name="b">Значение синего цвета</param>
        /// <returns>Цвет</returns>
        private System.Windows.Media.Color GetColor(byte r, byte g, byte b)
        {
            System.Windows.Media.Color color = new System.Windows.Media.Color();
            color.A = 255;
            color.R = r;
            color.G = g;
            color.B = b;
            return color;
        }

        private void exitBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Создаёт изображение из файл с данными
        /// </summary>
        private void CreateImg()
        {
            logger.Add("ВИЗУАЛИЗАТОР >> Визуализация данных...");
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int width, heigth;
            double max = double.MinValue, min = double.MaxValue;
            Color color;
            List<string> file = new List<string>();
            string[] str;
            int count = 0;
            int len = 0;
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Выбор файла спектрограммы";
            dialog.Filter = "txt file (*.txt)|*.txt";
            string sPath;
            if (dialog.ShowDialog() == true)
            {
                sPath = dialog.FileName;

                using (StreamReader sr = new StreamReader(sPath))
                {
                    logger.Add("ВИЗУАЛИЗАТОР >> Нахождение диапозона значения энергии...");
                    var st = new Stopwatch();
                    st.Start();
                    string line;
                    str = sr.ReadLine().Split(' ');
                    startPosition = Int32.Parse(str[0]);
                    lengthDepth = Int32.Parse(str[1]);
                    depthStartBox.Text = str[0];
                    depthFinishBox.Text = $"{startPosition + lengthDepth}";
                    str = sr.ReadLine().Split(' ');
                    nfft = Int64.Parse(str[0]);
                    maxFreq = Int32.Parse(str[1]);

                    while ((line = sr.ReadLine()) != null)
                    {
                        count++;
                        if (line[line.Length - 1] == ' ')
                            line = line.Remove(line.Length - 1, 1);
                        str = line.Split(' ');
                        if (len < str.Length)
                            len = str.Length;
                        for (int i = 0; i < str.Length; i++)
                        {
                            var d = Convert.ToDouble(str[i]);
                            if (d > max)
                            {
                                max = d;
                            }
                            if (d < min)
                            {
                                min = d;
                            }
                        }
                    }
                    st.Stop();
                    logger.Add($"ВИЗУАЛИЗАТОР >> Диапазон найден: от {min} до {max}");
                    logger.Add($"ВИЗУАЛИЗАТОР >> Нахождение заняло: {st.Elapsed}");
                }
                heigth = count;
                width = len;
                Bitmap bmp = new Bitmap(width, heigth);
                int y = 0;
                using (var sr = new StreamReader(sPath))
                {
                    logger.Add("ВИЗУАЛИЗАТОР >> Создание изображения из файла...");
                    var st = new Stopwatch();
                    st.Start();
                    string line = sr.ReadLine();
                    sr.ReadLine();
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line[line.Length - 1] == ' ')
                            line = line.Remove(line.Length - 1, 1);
                        str = line.Split(' ');
                        for (int x = 0; x < str.Length; x++)
                        {
                            color = GetColors(min, max, Convert.ToDouble(str[x]));
                            bmp.SetPixel(x, y, color);
                        }
                        y++;
                    }
                    st.Stop();
                    logger.Add("ВИЗУАЛИЗАТОР >> Изображение создано");
                    logger.Add($"ВИЗУАЛИЗАТОР >> Создание изображения заняло: {st.Elapsed}");
                }

                bmp.Save("Pic.png");
                bmp.Dispose();
                watch.Stop();
                logger.Add("ВИЗУАЛИЗАТОР >> Изображение сохранено: Pic.png");
                ShowImg();
                logger.Add($"ВИЗУАЛИЗАТОР >> Визуализация заняла: {watch.Elapsed}");
                logger.Add("");
                GC.Collect(1, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Приведение размерности входных данных до ближайшей степени двойки
        /// </summary>
        /// <param name="soundLine">Входные данные</param>
        /// <param name="K">Степень</param>
        /// <returns>Новые данные</returns>
        private short[] PowOfTwo(short[] soundLine, ref short K)
        {
            writeLogger.Add("ПОТОК ЧТЕНИЯ ДАННЫХ >> Приводим размерность входных данных к степени двойки...");
            int len = soundLine.Length;
            uint pow = 2;
            short k = 1;
            while (len > pow)
            {
                pow *= 2;
                k++;
            }

            K = k;
            if (len == pow)
                return soundLine;
            else
            {
                short[] newMas = new short[pow];
                for (int i = 0; i < pow; i++)
                {
                    if (i < len)
                        newMas[i] = soundLine[i];
                    else
                        newMas[i] = 0;
                }

                writeLogger.Add("ПОТОК ЧТЕНИЯ ДАННЫХ >> Размерность данных приведена к степени двойки! Показатель степени: " + K.ToString());
                return newMas;
            }
        }

        /// <summary>
        /// Чтение файлов для FFT
        /// </summary>
        /// <param name="files">Список файлов</param>
        private void ReaderFile(string[] files)
        {
            int i = 1;
            byte[] byteMas = null;
            short[] soundLine = null;
            byte[] b = new byte[2];
            short K = 0; //Показатель степени
            while (true)
            {
                if (isRequested)
                {
                    if (i <= files.Length)
                    {
                        writeLogger.Add($"ПОТОК ЧТЕНИЯ ДАННЫХ >> Чтение данных из {i} файла...");
                        var sw = new Stopwatch();
                        sw.Start();
                        byteMas = File.ReadAllBytes(files[i - 1]);
                        if (soundLine == null)
                        {
                            soundLine = new short[byteMas.Length / 2];
                        }
                        for (int j = 0; j < byteMas.Length; j += 2)
                        {
                            b[0] = byteMas[j];
                            b[1] = byteMas[j + 1];
                            soundLine[j / 2] = BitConverter.ToInt16(b, 0);
                        }
                        fileBuffer = (short[])PowOfTwo(soundLine, ref K).Clone();
                        sw.Stop();
                        byteMas = null;
                        GC.Collect();
                        writeLogger.Add($"ПОТОК ЧТЕНИЯ ДАННЫХ >> Считываение данных из {i} файла заняло: {sw.Elapsed.ToString()}");
                        //logger.Add("");

                        isRequested = false;
                        isReady = true;

                        if (i == files.Length)
                            return;
                        else
                            i++;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Get new Index
        /// </summary>
        /// <param name="index">Текущий индекс</param>
        /// <param name="nfft">Коэф. сжатия</param>
        /// <returns>Индекс</returns>
        private int GetIndex(int index, int nfft)
        {
            if (index == 0)
                return -1;
            double idx = Math.Log((double)index * maxFreq / (double)(nfft - 0)) * bufSize / Math.Log(maxFreq);
            if (idx < 0.0)
                return -1;
            return (int)Math.Floor(idx);
        }

        /// <summary>
        /// Get new Index
        /// </summary>
        /// <param name="index">Текущий индекс</param>
        /// <param name="nfft">Коэф. сжатия</param>
        /// <returns>Индекс</returns>
        private int GetIndexLin(int index, int nfft)
        {
            return (int)Math.Floor((double)index * bufSize / nfft);
        }

        /// <summary>
        /// Вычисления
        /// </summary>
        private void Start()
        {
            logger.Add("Получаем список файлов...");
            logger.Add("");
            int buffSize = 0;
            string[] fileList; //список файлов
            fileList = Directory.GetFiles(path);
            short counter = 1;
            byte deviceID = 0; //Номер устройства
            cuFloatComplex[] h_data; //Данные в формате CUDA (комплексные) на хосте
            CudaDeviceVariable<cuFloatComplex> d_data; //Входные данные в формате видеокарты

            Stopwatch watch = new Stopwatch();
            Stopwatch stopwatch = new Stopwatch();

            int nfft; //Размерность сжатых данных
            float[] buffer;
            int[] numbers;
            int index;
            bool firstTime = true;
            string Path = $"Output {DateTime.Now.Day}_{DateTime.Now.Month}_{DateTime.Now.Year} {DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.txt";
            StreamWriter sw = new StreamWriter(Path, true);
            sw.WriteLine($"{startPosition} {lengthDepth}");

            Task readTask = new Task(() => ReaderFile(fileList));
            readTask.Start();

            CudaContext ctx = new CudaContext(deviceID, CUCtxFlags.MapHost | CUCtxFlags.BlockingSync); //Создание контекста вычислений на устройстве

            while (true)
            {
                if (isReady)
                {
                    //Копирование
                    stopwatch.Reset();
                    stopwatch.Start();
                    buffSize = fileBuffer.Length;
                    
                    //Вычисления
                    h_data = new cuFloatComplex[buffSize];

                    for (int j = 0; j < buffSize; j++)
                    {
                        h_data[j].real = (float)Convert.ToDouble(fileBuffer[j]);
                        h_data[j].imag = 0;
                    }

                    isRequested = true;
                    isReady = false;

                    d_data = new CudaDeviceVariable<cuFloatComplex>(buffSize); //Создание переменной на видеокарте и выделение памяти под неё
                    d_data.CopyToDevice(h_data); //Копирование входных данных на видеокарту
                    CudaFFTPlan1D plan = new CudaFFTPlan1D(d_data.Size, cufftType.C2C, 1); //Создание плана вычислений !!!Найти и расписать пояснение подробнее

                    watch.Reset();
                    watch.Start();
                    plan.Exec(d_data.DevicePointer, TransformDirection.Forward); //Выполнение вычислений
                    watch.Stop();
                    h_data = d_data;

                    logger.Add($"ПОТОК ОБРАБОТКИ ДАННЫХ >> Вычисление {counter} дорожки на GPU заняло: {watch.Elapsed.ToString()}");

                    //Сжатие данных
                    nfft = h_data.Length / 2 + 1;
                    buffer = new float[bufSize];
                    numbers = new int[bufSize];
                    if (firstTime)
                    {
                        sw.WriteLine($"{nfft} {maxFreq}");
                        firstTime = false;
                    }

                    watch.Reset();
                    watch.Start();
                    int count = 0;
                    for (int j = 0; j < nfft; j++) //до nfft
                    {
                        index = GetIndex(j, nfft); //nfft
                        if (index < 0)
                            count++;
                        if (index >= 0)
                        {
                            buffer[index] += (float)Math.Sqrt(Math.Pow(h_data[j].real / (double)nfft, 2) + Math.Pow(h_data[counter].imag / (double)nfft, 2));
                            numbers[index]++;
                        }
                    }

                    for (int j = 0; j < bufSize; j++)
                    {
                        if (buffer[j] > 0)
                        {
                            buffer[j] /= (float)numbers[j];
                        }
                        else
                            buffer[j] = -1.0f;
                    }
                    watch.Stop();
                    logger.Add($"ПОТОК ОБРАБОТКИ ДАННЫХ >> Сжатие {counter} дорожки заняло: {watch.Elapsed.ToString()}");

                    //Запись готовых данных в файл
                    foreach (var item in buffer)
                    {
                        sw.Write(item + " ");
                    }
                    sw.WriteLine();

                    count++;
                    buffer = null;
                    numbers = null;
                    h_data = null;
                    d_data.Dispose();
                    plan.Dispose();
                    GC.Collect();

                    progressBar.Value++;
                    stopwatch.Stop();
                    logger.Add($"ПОТОК ОБРАБОТКИ ДАННЫХ >> Вычисление {counter++} дорожки заняло: {stopwatch.Elapsed.ToString()}");
                    //logger.Add("");
                }

                if (readTask.Status == TaskStatus.RanToCompletion && !isReady)
                {
                    readTask.Dispose();
                    break;
                }
            }
            CudaContext.ProfilerStop();
            ctx.Dispose();
            sw.Close();
        }

        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            logger.Add($"Начало вычислений: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add("");
            sw.Start();

            //Вычисления на GPU
            Start();

            sw.Stop();
            logger.Add($"Конец вычислений: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add($"Вычисления заняли: {sw.Elapsed.ToString()}");
            logger.Add("");


            //Создание изображения
            //CreateImg();  
        }

        /// <summary>
        /// Отображение изображения
        /// </summary>
        private void ShowImg()
        {
            BitmapImage bm1 = new BitmapImage();
            bm1.BeginInit();
            bm1.UriSource = new Uri("Pic.png", UriKind.Relative);
            bm1.CacheOption = BitmapCacheOption.OnLoad;
            bm1.EndInit();
            bitmap = new WriteableBitmap(bm1);
            imageBox.Source = bitmap;
        }

        private static System.Drawing.Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return System.Drawing.Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return System.Drawing.Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return System.Drawing.Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return System.Drawing.Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return System.Drawing.Color.FromArgb(255, t, p, v);
            else
                return System.Drawing.Color.FromArgb(255, v, p, q);
        }

        private void startColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            StartColor = System.Drawing.Color.FromArgb(255, startColorPiker.SelectedColor.Value.R, startColorPiker.SelectedColor.Value.G, startColorPiker.SelectedColor.Value.B);
            logger.Add($"Начальный цвет выбран: {StartColor.ToString()}");
            logger.Add("");
        }

        private void createImg_Click(object sender, RoutedEventArgs e)
        {
            CreateImg();
        }

        private void addColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ColorPicker colorPicker = new ColorPicker(colorData, logPath);
            colorPicker.ShowDialog();
            if (colorPicker.DialogResult == true)
            {
                colorData = colorPicker.colorData;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logger.Add($"Завершение работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Flush();
            logger.Close();
            writeLogger.Add($"Завершение работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            writeLogger.Flush();
            writeLogger.Close();
        }

        /// <summary>
        /// Проверяет, входит ли точка в заданную область
        /// </summary>
        /// <param name="x">Координата X заданной точки</param>
        /// <param name="y">Координата Y заданной точки</param>
        /// <param name="xS">Координата X начальной точки области</param>
        /// <param name="xF">Координата X конечной точки области</param>
        /// <param name="yS">Координата Y начальной точки области</param>
        /// <param name="yF">Координата Y конечной точки области</param>
        /// <returns>Входит или нет</returns>
        private bool IsInside(int x, int y, int xS, int xF, int yS, int yF)
        {
            var pointStart = imageBox.TranslatePoint(new System.Windows.Point(0, 0), this);
            var xNew = Math.Exp((double)x / bufSize * Math.Log(maxFreq));
            var yNew = y + startPosition;

            if (xNew >= xS && xNew <= xF && yNew >= yS && yNew <= yF)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Возращает затемнённую версию цвета
        /// </summary>
        /// <param name="color">Цвет</param>
        /// <returns>Затемнённая версия</returns>
        private Color GetDarker(Color color)
        {
            double r, g, b;
            double H = color.GetHue(), S = color.GetSaturation(), V = color.GetBrightness();
            V -= 0.25;
            if (V < 0)
                V = 0;
            var C = V * S;
            var X = C * (1 - Math.Abs(H / 60 % 2 - 1));
            var m = V - C;
            if (0 <= H && H < 60)
            {
                r = C;
                g = X;
                b = 0;
            }
            else if (60 <= H && H < 120)
            {
                r = X;
                g = C;
                b = 0;
            }
            else if (120 <= H && H < 180)
            {
                r = 0;
                g = C;
                b = X;
            }
            else if (180 <= H && H < 240)
            {
                r = 0;
                g = X;
                b = C;
            }
            else if (240 <= H && H < 300)
            {
                r = X;
                g = 0;
                b = C;
            }
            else
            {
                r = C;
                g = 0;
                b = X;
            }

            r = 255 * (r + m);
            g = 255 * (g + m);
            b = 255 * (b + m);
            return Color.FromArgb(Convert.ToInt32(r), Convert.ToInt32(g), Convert.ToInt32(b));
        }

        private void selectBtn_Click(object sender, RoutedEventArgs e)
        {
            Bitmap b;
            using (var fs = new FileStream("Pic.png", FileMode.Open))
               b = new Bitmap(fs);
            int xS, xF, yS, yF;

            for (int i = 0; i < b.Width; i++)
                for (int j = 0; j < b.Height; j++)
                {
                    xS = Int32.Parse(xSBox.Text);
                    xF = Int32.Parse(xFBox.Text);
                    yS = Int32.Parse(ySBox.Text);
                    yF = Int32.Parse(yFBox.Text);
                    if (!IsInside(i, j, xS, xF, yS, yF))
                    {
                        b.SetPixel(i, j, GetDarker(b.GetPixel(i, j)));
                    }
                }
            b.Save("Pic.png");
            b.Dispose();
            ShowImg();
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            int offset = 300;
            Bitmap b, bitmapNew;
            using (var fs = new FileStream("Pic.png", FileMode.Open))
                b = new Bitmap(fs);
            bitmapNew = new Bitmap(b.Width + 2 * offset, b.Height + 2 * offset);

            //Заглушка
            startPosition = 130;
            maxFreq = 10000.0;

            for (int x = 0; x < b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                {
                    bitmapNew.SetPixel(x + offset, y + offset, b.GetPixel(x, y));
                }

            var yx = Convert.ToInt32(offset + b.Height + offset * 0.25);
            List<int> helpList = new List<int>();
            for (int i = 0; i < (int)(maxFreq / 1000); i++)
                helpList.Add(Convert.ToInt32(Math.Log((i + 1) * 1000) * bufSize / Math.Log(maxFreq)));
            List<int> helpList2 = new List<int>();
            int value;
            for (int i = 0; i < (int)(maxFreq / 100); i++)
            {
                value = Convert.ToInt32(Math.Log((i + 1) * 100) * bufSize / Math.Log(maxFreq));
                if (value < b.Width * 0.9)
                {
                    helpList2.Add(value);
                }
                else
                    break;
            }
            List<int> helpList3 = new List<int>();
            for (int i = 0; i < (int)(maxFreq / 10); i++)
            {
                value = Convert.ToInt32(Math.Log((i + 1) * 10) * bufSize / Math.Log(maxFreq));
                if (value < b.Width * 0.7)
                {
                    helpList3.Add(value);
                }
                else
                    break;
            }
            List<int> helpList4 = new List<int>();
            for (int i = 0; i < (int)(maxFreq); i++)
            {
                value = Convert.ToInt32(Math.Log((i + 1)) * bufSize / Math.Log(maxFreq));
                if (value < b.Width * 0.25)
                {
                    helpList4.Add(value);
                }
                else
                    break;
            }


            for (int x = offset; x <= b.Width + offset; x++)
            {
                bitmapNew.SetPixel(x, yx , Color.Black);
                bitmapNew.SetPixel(x, yx + 1, Color.Black);
                bitmapNew.SetPixel(x, yx + 2, Color.Black);
                bitmapNew.SetPixel(x, yx + 3, Color.Black);
                bitmapNew.SetPixel(x, yx + 4, Color.Black);

                if (helpList.Count > 0)
                    if (x - offset >= helpList[0])
                    {
                        DrawV(ref bitmapNew, yx, x, offset, 1000);
                        helpList.Remove(helpList[0]);
                    }
                if (helpList2.Count > 0)
                    if (x - offset >= helpList2[0])
                    {
                        DrawV(ref bitmapNew, yx, x, offset, 100);
                        helpList2.Remove(helpList2[0]);
                    }
                if (helpList3.Count > 0)
                    if (x-offset>=helpList3[0])
                    {
                        DrawV(ref bitmapNew, yx, x, offset, 10);
                        helpList3.Remove(helpList3[0]);
                    }
                if (helpList4.Count > 0)
                    if (x - offset >= helpList4[0])
                    {
                        DrawV(ref bitmapNew, yx, x, offset);
                        helpList4.Remove(helpList4[0]);
                    }
            }

            var xy = Convert.ToInt32(offset * 0.75);
            for (int y = offset; y <= b.Height + offset; y++)
            {
                bitmapNew.SetPixel(xy, y, Color.Black);
                bitmapNew.SetPixel(xy + 1, y, Color.Black);
                bitmapNew.SetPixel(xy + 2, y, Color.Black);
                bitmapNew.SetPixel(xy + 3, y, Color.Black);
                bitmapNew.SetPixel(xy + 4, y, Color.Black);

                if ((y + startPosition) % 100 == 0)
                {
                    DrawH(ref bitmapNew, y, xy, offset, 100);
                }
                else if ((y + startPosition) % 10 == 0)
                {
                    DrawH(ref bitmapNew, y, xy, offset);
                }
            }

            bitmapNew.Save("New.png");
            bitmapNew.Dispose();
        }

        /// <summary>
        /// Отрисовка горизонтальных шкал
        /// </summary>
        /// <param name="bitmap">Изображение, на котором нужны шкалы</param>
        /// <param name="y">Координата Y</param>
        /// <param name="xy">Координата X</param>
        /// <param name="offset">Отступ</param>
        /// <param name="type">Шаг</param>
        private void DrawH(ref Bitmap bitmap, int y, int xy,int offset, int type = 10)
        {
            switch (type)
            {
                case 10:
                    {
                        int help = Convert.ToInt32(0.1 * offset);
                        for (int x = xy - help; x < xy + help; x++)
                        {
                            bitmap.SetPixel(x, y - 1, Color.Black);
                            bitmap.SetPixel(x, y, Color.Black);
                            bitmap.SetPixel(x, y + 1, Color.Black);
                        }
                        
                        break;
                    }
                case 100:
                    {
                        int help = Convert.ToInt32(0.15 * offset);
                        for (int x = xy - help; x < xy + help; x++)
                        {
                            bitmap.SetPixel(x, y - 2, Color.Black);
                            bitmap.SetPixel(x, y - 1, Color.Black);
                            bitmap.SetPixel(x, y, Color.Black);
                            bitmap.SetPixel(x, y + 1, Color.Black);
                            bitmap.SetPixel(x, y + 2, Color.Black);
                        }
                        break;
                    }
            }
        }

        private void DrawV(ref Bitmap bitmap, int yx, int x, int offset, int type = 1)
        {
            switch (type)
            {
                case 1:
                    {
                        int help = Convert.ToInt32(0.05 * offset);
                        for (int y = yx - help; y < yx + help; y++)
                        {
                            bitmap.SetPixel(x - 1, y, Color.Black);
                            bitmap.SetPixel(x, y, Color.Black);
                            bitmap.SetPixel(x + 1, y, Color.Black);
                        }

                        break;
                    }
                case 10:
                    {
                        int help = Convert.ToInt32(0.1 * offset);
                        for (int y = yx - help; y < yx + help; y++)
                        {
                            bitmap.SetPixel(x - 1, y, Color.Black);
                            bitmap.SetPixel(x, y, Color.Black);
                            bitmap.SetPixel(x + 1, y, Color.Black);
                        }

                        break;
                    }
                case 100:
                    {
                        int help = Convert.ToInt32(0.15 * offset);
                        for (int y = yx - help; y < yx + help; y++)
                        {
                            bitmap.SetPixel(x - 2, y, Color.Black);
                            bitmap.SetPixel(x - 1, y, Color.Black);
                            bitmap.SetPixel(x, y, Color.Black);
                            bitmap.SetPixel(x + 1, y, Color.Black);
                            bitmap.SetPixel(x + 2, y, Color.Black);
                        }
                        break;
                    }
                case 1000:
                    {
                        int help = Convert.ToInt32(0.2 * offset);
                        for (int y = yx - help; y < yx + help; y++)
                        {
                            bitmap.SetPixel(x - 3, y, Color.Black);
                            bitmap.SetPixel(x - 2, y, Color.Black);
                            bitmap.SetPixel(x - 1, y, Color.Black);
                            bitmap.SetPixel(x, y, Color.Black);
                            bitmap.SetPixel(x + 1, y, Color.Black);
                            bitmap.SetPixel(x + 2, y, Color.Black);
                            bitmap.SetPixel(x + 3, y, Color.Black);
                        }
                        break;
                    }
            }
        }

        private void finishColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            FinishColor = System.Drawing.Color.FromArgb(255, finishColorPiker.SelectedColor.Value.R, finishColorPiker.SelectedColor.Value.G, finishColorPiker.SelectedColor.Value.B);
            logger.Add($"Конечный цвет выбран: {FinishColor.ToString()}");
            logger.Add("");
        }

        /// <summary>
        /// Разложение TDMS файла на бинарные файлы
        /// </summary>
        private void TDMS()
        {
            using (var fstream = new FileStream(tdmsPath, FileMode.Open))
            {
                byte[] b4 = new byte[4];
                FileStream[] streams = new FileStream[lengthDepth];
                for (int j = 0; j < lengthDepth; j++)
                {
                    streams[j] = new FileStream($"{path}\\data{j}", FileMode.Create);
                }

                fstream.Seek(dataOffset, SeekOrigin.Begin);
                int i = 0;
                ulong l = 0;
                int count = 0;
                while ((count = fstream.Read(b4, 0, 4)) > 0)
                {
                    try
                    {
                       
                        l += 4;
                        streams[i].Write(b4, 0, 4);
                        if (i == lengthDepth - 1)
                            i = 0;
                        else
                            i++;
                    }
                    catch
                    {
                        
                        break;
                    }
                }
                for (int j = 0; j < lengthDepth; j++)
                {
                    streams[j].Close();
                }
                fstream.Close();
            }
        }

        private void openTDMS_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Выбор TDMS файла";
            dialog.Filter = "TDMS file (*.tdms)|*.tdms|All files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                tdmsPath = dialog.FileName;
            }
            if (path != "" && path != null)
            {
                using (FileStream fstream = new FileStream(tdmsPath, FileMode.Open))
                {
                    byte[] b4 = new byte[4];
                    byte[] b8 = new byte[8];

                    // Значению смешения самих данных
                    fstream.Seek(20, SeekOrigin.Current);
                    fstream.Read(b8, 0, 8);
                    dataOffset = 28 + Convert.ToInt64(BitConverter.ToUInt64(b8, 0)); // Data Offset in the file

                    // Значению самих данных
                    fstream.Seek(4, SeekOrigin.Current);
                    fstream.Read(b4, 0, 4);
                    var num = BitConverter.ToUInt32(b4, 0);
                    byte[] str = new byte[num];
                    fstream.Read(str, 0, str.Length);
                    var name = System.Text.Encoding.Default.GetString(str);
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Нет данных
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Количество свойств

                    // Имя
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Длина имени свойства
                    fstream.Seek(num + 4, SeekOrigin.Current);
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Длина строки значения
                    fstream.Seek(num, SeekOrigin.Current);

                    //Частота
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Длина имени свойства
                    fstream.Seek(num + 4, SeekOrigin.Current);
                    fstream.Read(b8, 0, 8);
                    var dnum = BitConverter.ToDouble(b8, 0); // значение
                    maxFreq = Convert.ToDouble(dnum);

                    // Шаг глубины
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Длина имени свойства
                    fstream.Seek(num + 4, SeekOrigin.Current);
                    fstream.Read(b8, 0, 8);
                    dnum = BitConverter.ToDouble(b8, 0); // значение
                    step = Convert.ToInt32(dnum);

                    // Начальное значение глубины
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Длина имени свойства
                    fstream.Seek(num + 4, SeekOrigin.Current);
                    fstream.Read(b4, 0, 4);
                    dnum = BitConverter.ToUInt32(b4, 0); // значение
                    startPosition = Convert.ToInt32(dnum);
                    depthStartBox.Text = Convert.ToString(startPosition);

                    // глубина
                    fstream.Read(b4, 0, 4);
                    num = BitConverter.ToUInt32(b4, 0); //Длина имени свойства
                    fstream.Seek(num + 4, SeekOrigin.Current);
                    fstream.Read(b4, 0, 4);
                    dnum = BitConverter.ToUInt32(b4, 0); // значение
                    lengthDepth = Convert.ToInt32(dnum);
                    depthFinishBox.Text = Convert.ToString(startPosition + lengthDepth);

                    FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                    folderBrowserDialog.Description = "Выберите папку сохранения результатов разбиения TMDS файла.\nУбедитесь, что папка пустая!";
                    if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        path = folderBrowserDialog.SelectedPath;

                        Task task = new Task(() => TDMS());
                        task.Start();
                    }
                    fstream.Close();
                }
            }
        }

        private void openBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                logger.Add("Ожидается выбор папки...");
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    path = folderBrowserDialog.SelectedPath;
                    fileCount = Directory.GetFiles(path).Length;
                    depthFinishBox.Text = Convert.ToString(fileCount);
                    progressBar.Value = 0;
                    progressBar.Maximum = fileCount;
                    logger.Add($"Папка выбрана: {path}");
                    logger.Add($"Файлов в папке: {fileCount.ToString()}");
                    logger.Add("");
                }
                else
                {
                    logger.Add("Папка не выбрана!");
                    logger.Add("");
                }
            }
            catch
            {
                logger.Add("Ошибка при открытии папки!");
                logger.Add("");
                System.Windows.MessageBox.Show("Ошибка при открытии папки!");
            }
        }

        private void imageBox_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pointStart = imageBox.TranslatePoint(new System.Windows.Point(0,0),this);
            var pointFinish = new System.Windows.Point(imageBox.ActualWidth, imageBox.ActualHeight);
            var mousePoint = new System.Windows.Point(e.GetPosition(null).X, e.GetPosition(null).Y);
            var xNew = Math.Exp((mousePoint.X - pointStart.X) / imageBox.ActualWidth * Math.Log(maxFreq));
            var yNew = (mousePoint.Y - pointStart.Y) / imageBox.ActualHeight;
            XLabel.Content = $"Частота: {Convert.ToInt32(xNew)} Гц";
            YLabel.Content = $"Глубина: {startPosition + Convert.ToInt32(yNew * (lengthDepth))} метров";
        }

        /// <summary>
        /// Вычисление цвета пикселя
        /// </summary>
        /// <param name="min">Минимальное значение</param>
        /// <param name="max">Максимальное значение</param>
        /// <param name="current">Текущее значение</param>
        /// <returns>Цвет</returns>
        private System.Drawing.Color GetColors(double min, double max, double current)
        {
            //double hStart = StartColor.GetHue();
            //double hFinish = FinishColor.GetHue();
            //var h = hFinish - hStart;
            //h = h * help + min;
            //color = ColorFromHSV(h, 1, 1);

            Color color1 = StartColor, color2 = FinishColor;
            double MIN = min, MAX = max;
            foreach (var item in colorData.ColorList)
            {
                if (current >= item.Start && current <=item.Finish)
                {
                    color1 = item.StartColor;
                    if (current == -1.0)
                        return color1;
                    color2 = item.FinishColor;
                    MIN = item.Start;
                    MAX = item.Finish;
                    break;
                }
            }
            
            var help = (current - MIN)/ (MAX - MIN);

            var dR = Convert.ToInt32(Math.Ceiling(help * (color2.R - color1.R)));
            var dG = Convert.ToInt32(Math.Ceiling(help * (color2.G - color1.G)));
            var dB = Convert.ToInt32(Math.Ceiling(help * (color2.B - color1.B)));
            return Color.FromArgb(dR + color1.R, dG + color1.G, dB + color1.B);
        }
    }
}
