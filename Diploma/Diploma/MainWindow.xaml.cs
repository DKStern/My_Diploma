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
        private string logPath, readyDataPath;
        private long nfft;
        private int offset = 300; //Offset for Scales
        private Bitmap[] numbers = new Bitmap[10];
        private Bitmap mainBitMap;

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
            MakeNumbers();
        }

        private void MakeNumbers()
        {
            numbers[0] = new Bitmap(Numbers._0);
            numbers[1] = new Bitmap(Numbers._1);
            numbers[2] = new Bitmap(Numbers._2);
            numbers[3] = new Bitmap(Numbers._3);
            numbers[4] = new Bitmap(Numbers._4);
            numbers[5] = new Bitmap(Numbers._5);
            numbers[6] = new Bitmap(Numbers._6);
            numbers[7] = new Bitmap(Numbers._7);
            numbers[8] = new Bitmap(Numbers._8);
            numbers[9] = new Bitmap(Numbers._9);
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
        /// <param name="hasPath">Есть ли путь</param>
        private void CreateImg(bool hasPath)
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
            string line;

            if (!hasPath)
            {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = "Выбор файла спектрограммы";
                dialog.Filter = "txt file (*.txt)|*.txt";
                if (dialog.ShowDialog() == true)
                {
                    readyDataPath = dialog.FileName;
                }
            }

            if (readyDataPath != null)
            {
                using (StreamReader sr = new StreamReader(readyDataPath))
                {
                    logger.Add("ВИЗУАЛИЗАТОР >> Нахождение диапозона значения энергии...");
                    var st = new Stopwatch();
                    st.Start();
                    str = sr.ReadLine().Split(' ');
                    startPosition = Int32.Parse(str[0]);
                    lengthDepth = Int32.Parse(str[1]);
                    depthStartBox.Text = str[0];
                    depthFinishBox.Text = $"{startPosition + lengthDepth}";
                    str = sr.ReadLine().Split(' ');
                    nfft = Int64.Parse(str[0]); //возможно лишнее
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

                heigth = count + 2 * offset;
                width = len + 2 * offset;
                Bitmap bmp = new Bitmap(width, heigth);
                using (var sr = new StreamReader(readyDataPath))
                {
                    logger.Add("ВИЗУАЛИЗАТОР >> Создание изображения из файла...");
                    var st = new Stopwatch();
                    st.Start();
                    sr.ReadLine();
                    sr.ReadLine();
                    int y = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line[line.Length - 1] == ' ')
                            line = line.Remove(line.Length - 1, 1);
                        str = line.Split(' ');
                        for (int x = 0; x < str.Length; x++)
                        {
                            color = GetColors(min, max, Convert.ToDouble(str[x]));
                            bmp.SetPixel(x + offset, y + offset, color);
                        }
                        y++;
                    }

                    //Добавление шкал
                    var yx = Convert.ToInt32(bmp.Height - offset * 0.75);
                    List<int> helpList = new List<int>();
                    for (int i = 0; i < (int)(maxFreq / 1000); i++)
                        helpList.Add(Convert.ToInt32(Math.Log((i + 1) * 1000) * bufSize / Math.Log(maxFreq)));
                    List<int> helpList2 = new List<int>();
                    int value;
                    for (int i = 0; i < (int)(maxFreq / 100); i++)
                    {
                        value = Convert.ToInt32(Math.Log((i + 1) * 100) * bufSize / Math.Log(maxFreq));
                        if (value < (bmp.Width - 2 * offset) * 0.8)
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
                        if (value < (bmp.Width - 2 * offset) * 0.55)
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
                        if (value < (bmp.Width - 2 * offset) * 0.25)
                        {
                            helpList4.Add(value);
                        }
                        else
                            break;
                    }

                    bool b = true;
                    for (int x = bmp.Width - offset; x >= offset; x--)
                    {
                        bmp.SetPixel(x, yx, Color.Black);
                        bmp.SetPixel(x, yx + 1, Color.Black);
                        bmp.SetPixel(x, yx + 2, Color.Black);
                        bmp.SetPixel(x, yx + 3, Color.Black);
                        bmp.SetPixel(x, yx + 4, Color.Black);

                        b = true;
                        if (helpList.Count > 0)
                            if (x - offset <= helpList[helpList.Count - 1])
                            {
                                DrawV(ref bmp, yx, x, offset, 1000, helpList.Count * 1000, b);
                                helpList.Remove(helpList[helpList.Count - 1]);
                                b = false;
                            }
                        if (helpList2.Count > 0)
                            if (x - offset <= helpList2[helpList2.Count - 1])
                            {

                                DrawV(ref bmp, yx, x, offset, 100, helpList2.Count * 100, b);
                                helpList2.Remove(helpList2[helpList2.Count - 1]);
                                b = false;
                            }
                        if (helpList3.Count > 0)
                            if (x - offset <= helpList3[helpList3.Count - 1])
                            {
                                DrawV(ref bmp, yx, x, offset, 10, helpList3.Count * 10, b);
                                helpList3.Remove(helpList3[helpList3.Count - 1]);
                                b = false;
                            }
                        if (helpList4.Count > 0)
                            if (x - offset <= helpList4[helpList4.Count - 1])
                            {
                                DrawV(ref bmp, yx, x, offset, 1, helpList4.Count, b);
                                helpList4.Remove(helpList4[helpList4.Count - 1]);
                                b = false;
                            }
                    }

                    var xy = Convert.ToInt32(offset * 0.75);
                    int of;
                    for (int yy = offset; yy <= bmp.Height - offset; yy++)
                    {
                        bmp.SetPixel(xy, yy, Color.Black);
                        bmp.SetPixel(xy + 1, yy, Color.Black);
                        bmp.SetPixel(xy + 2, yy, Color.Black);
                        bmp.SetPixel(xy + 3, yy, Color.Black);
                        bmp.SetPixel(xy + 4, yy, Color.Black);

                        of = yy - offset + startPosition;
                        if ((of) % 100 == 0)
                        {
                            DrawH(ref bmp, yy, xy, offset, 100, of);
                        }
                        else if ((of) % 10 == 0)
                        {
                            DrawH(ref bmp, yy, xy, offset, 10, of);
                        }
                    }


                    st.Stop();
                    logger.Add("ВИЗУАЛИЗАТОР >> Изображение создано");
                    logger.Add($"ВИЗУАЛИЗАТОР >> Создание изображения заняло: {st.Elapsed}");
                }

                bmp.Save($"{logPath}\\Pic.png");
                mainBitMap = new Bitmap(bmp);
                bmp.Dispose();
                watch.Stop();
                logger.Add("ВИЗУАЛИЗАТОР >> Изображение сохранено: Pic.png");
                ShowImg();
                logger.Add($"ВИЗУАЛИЗАТОР >> Визуализация заняла: {watch.Elapsed}");
                logger.Add("");
                logger.Flush();
                GC.Collect(1, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                selectBox.IsEnabled = true;
                saveBtn.IsEnabled = true;
            }
            else
            {
                System.Windows.MessageBox.Show("Вы не выбрали файл с данными!");
                return;
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
            readyDataPath = $"{logPath}\\Output.txt";
            StreamWriter sw = new StreamWriter(readyDataPath, true);
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
                    logger.Flush();
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
            CreateImg(true);  
        }

        /// <summary>
        /// Отображение изображения
        /// </summary>
        private void ShowImg()
        {
            BitmapImage bm1 = new BitmapImage();
            bm1.BeginInit();
            bm1.UriSource = new Uri($"{logPath}\\Pic.png", UriKind.Relative);
            bm1.CacheOption = BitmapCacheOption.OnLoad;
            bm1.EndInit();
            bitmap = new WriteableBitmap(bm1);
            imageBox.Source = bitmap;
        }

        private void startColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            StartColor = System.Drawing.Color.FromArgb(255, startColorPiker.SelectedColor.Value.R, startColorPiker.SelectedColor.Value.G, startColorPiker.SelectedColor.Value.B);
            logger.Add($"Начальный цвет выбран: {StartColor.ToString()}");
            logger.Add("");
        }

        private void createImg_Click(object sender, RoutedEventArgs e)
        {
            CreateImg(false);
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
            x -= offset;
            y -= offset;
            var pointStart = imageBox.TranslatePoint(new System.Windows.Point(0, 0), this);
            var xNew = Math.Exp((double)x / bufSize * Math.Log(maxFreq));
            var yNew = y + startPosition;

            if (xNew >= xS && xNew <= xF && yNew >= yS && yNew <= yF)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Выделение зоны
        /// </summary>
        /// <param name="color">Цвет</param>
        /// <returns>Затемнённая версия</returns>
        private Color GetDarker(Color color)
        {
            double r, g, b;
            double H = color.GetHue(), S = color.GetSaturation(), V = color.GetBrightness();
            S -= 0.5;
            if (S < 0)
                S = 0;
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
            int xS, xF, yS, yF, iS = startPosition;
            double min = double.MaxValue, max = double.MinValue;
            xS = int.Parse(xSBox.Text);
            xF = int.Parse(xFBox.Text);
            yS = int.Parse(ySBox.Text);
            yF = int.Parse(yFBox.Text);
            if (yS < startPosition)
            {
                System.Windows.MessageBox.Show($"Начальная глубина не может быть меньше {startPosition}!");
                return;
            }
            if (yF <= yS)
            {
                System.Windows.MessageBox.Show("Конечная глубина не может быть меньше/равна начальной глубине!");
                return;
            }
            if (yF > startPosition + lengthDepth)
            {
                System.Windows.MessageBox.Show($"Конечная глубина не может быть больше {startPosition + lengthDepth}!");
                return;
            }
            if (xS < 0)
            {
                System.Windows.MessageBox.Show("Начальная частота не может быть меньше 0!");
                return;
            }
            if (xF <= xS)
            {
                System.Windows.MessageBox.Show("Конечная частота не может быть меньше/равна начальной частоте!");
                return;
            }
            if (xF > maxFreq)
            {
                System.Windows.MessageBox.Show($"Конечная частота не может быть больше {maxFreq} Гц!");
                return;
            }
            var xNewS = Math.Log(xS) * bufSize / Math.Log(maxFreq);
            var xNewF = Math.Log(xF) * bufSize / Math.Log(maxFreq);
            var xNew = Convert.ToInt32(xNewF - xNewS);
            Bitmap b = new Bitmap(mainBitMap), helpBitMap = new Bitmap(xNew + 1, yF - yS + 1);
            StreamReader sr = new StreamReader(readyDataPath);
            sr.ReadLine();
            sr.ReadLine();
            string[] sMas;
            string line;
            while (!sr.EndOfStream)
            {
                line = sr.ReadLine();
                if (iS >= yS && iS <= yF)
                {
                    if (line[line.Length - 1] == ' ')
                        line = line.Remove(line.Length - 1, 1);
                    sMas = line.Split(' ');
                    for (int i = Convert.ToInt32(xNewS); i <= Convert.ToInt32(xNewF); i++)
                    {
                        if (double.Parse(sMas[i]) < min)
                            min = double.Parse(sMas[i]);
                        if (double.Parse(sMas[i]) > max)
                            max = double.Parse(sMas[i]);
                    }
                }
                iS++;
            }
            sr.Dispose();
            sr.Close();

            sr = new StreamReader(readyDataPath);
            sr.ReadLine();
            sr.ReadLine();
            iS = startPosition;
            int h = 0, w = 0;
            while (!sr.EndOfStream)
            {
                line = sr.ReadLine();
                if (iS >= yS && iS <= yF)
                {
                    if (line[line.Length - 1] == ' ')
                        line = line.Remove(line.Length - 1, 1);
                    sMas = line.Split(' ');
                    for (int i = Convert.ToInt32(xNewS); i <= Convert.ToInt32(xNewF); i++)
                    {
                        helpBitMap.SetPixel(w, h, GetColors(min, max, double.Parse(sMas[i])));
                        w++;
                        if (w == helpBitMap.Width)
                        {
                            w = 0;
                            h++;
                        }
                    }
                }
                iS++;
            }
            sr.Dispose();
            sr.Close();


            int x = 0, y = 0;
            for (int i = offset; i < b.Width - offset; i++)
                for (int j = offset; j < b.Height - offset; j++)
                {
                    if (!IsInside(i, j, xS, xF, yS, yF))
                    {
                        b.SetPixel(i, j, GetDarker(b.GetPixel(i, j)));
                    }
                    else
                    {
                        b.SetPixel(i, j, helpBitMap.GetPixel(x, y));
                        if (y < yF - yS)
                            y++;
                        else
                        {
                            y = 0;
                            x++;
                        }
                    }
                }
            b.Save($"{logPath}\\Pic.png");
            b.Dispose();
            ShowImg();
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG file (*.png)|*.png";
            saveFileDialog.Title = "Сохранение спектрограммы";
            if (File.Exists($"{logPath}\\Pic.png"))
            {
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var path = saveFileDialog.FileName;
                    File.Copy($"{logPath}\\Pic.png", $"{path}");
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Нет данных для сохранения!");
            }
        }

        /// <summary>
        /// Отрисовка горизонтальных делений
        /// </summary>
        /// <param name="bitmap">Изображение, на котором нужны шкалы</param>
        /// <param name="y">Координата Y</param>
        /// <param name="xy">Координата X</param>
        /// <param name="offset">Отступ</param>
        /// <param name="type">Шаг</param>
        /// <param name="number">Число</param>
        private void DrawH(ref Bitmap bitmap, int y, int xy,int offset, int type, int number)
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

                        int num, ofX = xy - help - 7, ofY = y, sizeX, sizeY;
                        while (number > 0)
                        {
                            num = number % 10;
                            number /= 10;
                            sizeX = numbers[num].Width;
                            sizeY = numbers[num].Height;
                            ofX = ofX - sizeX - 3;
                            ofY = y - sizeY / 2;
                            for (int i = ofX; i < ofX + sizeX; i++)
                                for (int j = ofY; j < ofY + sizeY; j++)
                                {
                                    bitmap.SetPixel(i, j, numbers[num].GetPixel(i - ofX, j - ofY));
                                }
                        }

                        break;
                    }
            }
        }

        private void xSBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            xSBox.Text = CheckNum(xSBox.Text);
        }

        private void xFBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            xFBox.Text = CheckNum(xFBox.Text);
        }

        private string CheckNum(string str)
        {
            int a;
            if (int.TryParse(str, out a))
            {
                return str;
            }
            else
            {
                for (int i=0;i<str.Length;i++)
                {
                    if (!char.IsNumber(str[i]))
                    {
                        str = str.Remove(i, 1);
                        i--;
                    }
                }
                return str;
            }
        }

        private void ySBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ySBox.Text = CheckNum(ySBox.Text);
        }

        private void yFBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            yFBox.Text = CheckNum(yFBox.Text);
        }

        /// <summary>
        /// Отрисовка вертикальных делений
        /// </summary>
        /// <param name="bitmap">Изображение, на котором нужны шкалы</param>
        /// <param name="yx">Координата Y</param>
        /// <param name="x">Координата X</param>
        /// <param name="offset">Отступ</param>
        /// <param name="type">Шаг</param>
        /// <param name="number">Чисдл</param>
        private void DrawV(ref Bitmap bitmap, int yx, int x, int offset, int type, int number, bool isNeeded)
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

                        if (isNeeded)
                        {
                            int w = number, edd = 0;
                            while (w > 0)
                            {
                                edd++;
                                w /= 10;
                            }
                            int num, ofX = x, ofY = yx + help + 5, sizeX, sizeY;
                            while (number > 0)
                            {
                                num = number % 10;
                                number /= 10;
                                sizeX = numbers[num].Width;
                                sizeY = numbers[num].Height;
                                ofX = x - sizeX / 2;
                                ofY = yx + help + 5 + edd * (sizeY + 3);
                                for (int i = ofX; i < ofX + sizeX; i++)
                                    for (int j = ofY; j < ofY + sizeY; j++)
                                    {
                                        bitmap.SetPixel(i, j, numbers[num].GetPixel(i - ofX, j - ofY));
                                    }
                                edd--;
                            }
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

                        if (isNeeded)
                        {
                            int w = number, edd = 0;
                            while (w > 0)
                            {
                                edd++;
                                w /= 10;
                            }
                            int num, ofX = x, ofY = yx + help + 5, sizeX, sizeY;
                            while (number > 0)
                            {
                                num = number % 10;
                                number /= 10;
                                sizeX = numbers[num].Width;
                                sizeY = numbers[num].Height;
                                ofX = x - sizeX / 2;
                                ofY = yx + help + 5 + edd * (sizeY + 3);
                                for (int i = ofX; i < ofX + sizeX; i++)
                                    for (int j = ofY; j < ofY + sizeY; j++)
                                    {
                                        bitmap.SetPixel(i, j, numbers[num].GetPixel(i - ofX, j - ofY));
                                    }
                                edd--;
                            }
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

                        if (isNeeded)
                        {
                            int w = number, edd = 0;
                            while (w > 0)
                            {
                                edd++;
                                w /= 10;
                            }
                            int num, ofX = x, ofY = yx + help + 5, sizeX, sizeY;
                            while (number > 0)
                            {
                                num = number % 10;
                                number /= 10;
                                sizeX = numbers[num].Width;
                                sizeY = numbers[num].Height;
                                ofX = x - sizeX / 2;
                                ofY = yx + help + 5 + edd * (sizeY + 3);
                                for (int i = ofX; i < ofX + sizeX; i++)
                                    for (int j = ofY; j < ofY + sizeY; j++)
                                    {
                                        bitmap.SetPixel(i, j, numbers[num].GetPixel(i - ofX, j - ofY));
                                    }
                                edd--;
                            }
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

                        int w = number, edd = 0;
                        while (w > 0)
                        {
                            edd++;
                            w /= 10;
                        }
                        int num, ofX = x, ofY = yx + help + 5, sizeX, sizeY;
                        while (number > 0)
                        {
                            num = number % 10;
                            number /= 10;
                            sizeX = numbers[num].Width;
                            sizeY = numbers[num].Height;
                            ofX = x - sizeX / 2;
                            ofY = yx + help + 5 + edd * (sizeY + 3);
                            for (int i = ofX; i < ofX + sizeX; i++)
                                for (int j = ofY; j < ofY + sizeY; j++)
                                {
                                    bitmap.SetPixel(i, j, numbers[num].GetPixel(i - ofX, j - ofY));
                                }
                            edd--;
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
                    startPosition = 0; // То, что в файле называется начало замера, на деле длина провода устройства, поэтому начало 0
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
                    startBtn.IsEnabled = true;
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
            var offsetH = (double)offset / (bufSize + 2 * offset);
            var pointStart = imageBox.TranslatePoint(new System.Windows.Point(0,0),this);
            var mousePoint = new System.Windows.Point(e.GetPosition(null).X, e.GetPosition(null).Y);
            var xNew = (mousePoint.X - pointStart.X) / imageBox.ActualWidth;
            var yNew = (mousePoint.Y - pointStart.Y) / imageBox.ActualHeight;
            if (xNew > offsetH && xNew < 1 - offsetH)
            {
                xNew = Math.Exp((mousePoint.X - pointStart.X - imageBox.ActualWidth * offsetH) / (imageBox.ActualWidth * (1 - 2 * offsetH)) * Math.Log(maxFreq));
                XLabel.Content = $"Частота: {Convert.ToInt32(xNew)} Гц";
            }
            else
                XLabel.Content = $"Частота: не наведено";
            var offsetV = (double)offset / (lengthDepth + 2 * offset);
            if (yNew > offsetV && yNew < 1 - offsetV)
            {
                yNew = (mousePoint.Y - pointStart.Y - imageBox.ActualHeight * offsetV) / (imageBox.ActualHeight * (1 - 2 * offsetV));
                YLabel.Content = $"Глубина: {startPosition + Convert.ToInt32(yNew * (lengthDepth))} метров";
            }
            else
                YLabel.Content = $"Глубина: не наведено";
        }

        /// <summary>
        /// Вычисление цвета пикселя
        /// </summary>
        /// <param name="min">Минимальное значение</param>
        /// <param name="max">Максимальное значение</param>
        /// <param name="current">Текущее значение</param>
        /// <returns>Цвет</returns>
        private Color GetColors(double min, double max, double current)
        {
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
