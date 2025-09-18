using NLog;
using System.Runtime.CompilerServices;


namespace DigicertMetadataSync.Logic
{
    public static class ListFlushExtensions
    {
        /// <summary>
        /// Add one item; if the buffer hits <paramref name="threshold"/>, write every item to trace,
        /// add to the running total, and clear the buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddAndMaybeFlush(this List<string> buffer,
            string item,
            Logger log,
            string label,
            ref int totalCount,
            int threshold = 1000)
        {
            buffer.Add(item);
            if (buffer.Count >= threshold)
                FlushToTrace(buffer, log, label, ref totalCount);
        }

        /// <summary>
        /// Flush any remaining items after a page/loop ends.
        /// </summary>
        public static void FlushRemainder(this List<string> buffer,
            Logger log,
            string label,
            ref int totalCount)
            => FlushToTrace(buffer, log, label, ref totalCount);

        private static void FlushToTrace(List<string> buffer,
            Logger log,
            string label,
            ref int totalCount)
        {
            if (buffer.Count == 0) return;

            // One line per item keeps logs searchable and prevents jumbo lines.
            foreach (var s in buffer)
                log.Trace("{Label}: {Item}", label, s);

            totalCount += buffer.Count;
            buffer.Clear(); // release memory
            buffer.TrimExcess(); // optional: shrink backing array
        }
    }

}
