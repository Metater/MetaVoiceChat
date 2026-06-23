using System.Text;

namespace Metater
{
    public class MetaCsv
    {
        private readonly StringBuilder csv = new();

        public MetaCsv(params string[] headers)
        {
            csv.AppendJoin(',', headers);
        }

        public void AddRow(params object[] values)
        {
            csv.AppendLine();
            csv.AppendJoin(',', values);
        }

        public void Flush()
        {
            csv.AppendLine();
            UnityEngine.Debug.Log(csv.ToString());
        }
    }
}