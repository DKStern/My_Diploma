namespace Diploma.Classes
{
    public class SelectedColor
    {
        public int Start { get; }
        public int Finish { get; }
        public System.Drawing.Color StartColor { get; }
        public System.Drawing.Color FinishColor { get; }

        /// <summary>
        /// Создаёт новый диапазон с заданными начальным и конечным цветами
        /// </summary>
        /// <param name="start">Начало диапазона (в процентах)</param>
        /// <param name="finish">Конец диапазона (в процентах)</param>
        /// /// <param name="startColor">Начальный цвет</param>
        /// <param name="finishColor">Конечный цвет</param>
        public SelectedColor(int start, int finish, System.Drawing.Color startColor, System.Drawing.Color finishColor)
        {
            Start = start;
            Finish = finish;
            StartColor = startColor;
            FinishColor = finishColor;
        }
    }
}
