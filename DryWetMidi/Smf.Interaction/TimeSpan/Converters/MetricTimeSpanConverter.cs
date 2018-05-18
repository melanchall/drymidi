﻿using System;
using System.Linq;

namespace Melanchall.DryWetMidi.Smf.Interaction
{
    internal sealed class MetricTimeSpanConverter : ITimeSpanConverter
    {
        #region ITimeSpanConverter

        public ITimeSpan ConvertTo(long timeSpan, long time, TempoMap tempoMap)
        {
            var ticksPerQuarterNoteTimeDivision = tempoMap.TimeDivision as TicksPerQuarterNoteTimeDivision;
            if (ticksPerQuarterNoteTimeDivision == null)
                throw new ArgumentException("Time division is not supported for time span conversion.", nameof(tempoMap));

            var startTimeSpan = TicksToMetricTimeSpan(time, ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote, tempoMap);
            var endTimeSpan = TicksToMetricTimeSpan(time + timeSpan, ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote, tempoMap);

            return endTimeSpan - startTimeSpan;
        }

        public long ConvertFrom(ITimeSpan timeSpan, long time, TempoMap tempoMap)
        {
            var ticksPerQuarterNoteTimeDivision = tempoMap.TimeDivision as TicksPerQuarterNoteTimeDivision;
            if (ticksPerQuarterNoteTimeDivision == null)
                throw new ArgumentException("Time division is not supported for time span conversion.", nameof(tempoMap));

            var startTimeSpan = TicksToMetricTimeSpan(time, ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote, tempoMap);
            var endTimeSpan = startTimeSpan + (MetricTimeSpan)timeSpan;

            return MetricTimeSpanToTicks(endTimeSpan, ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote, tempoMap) - time;
        }

        #endregion

        #region Methods

        private static MetricTimeSpan TicksToMetricTimeSpan(long time, short ticksPerQuarterNote, TempoMap tempoMap)
        {
            if (time == 0)
                return new MetricTimeSpan();

            var tempoLine = tempoMap.Tempo;
            var tempoValuesSortedArray = tempoLine.Values.ToArray();
            var nextTempoChangeIndex = Array.BinarySearch(tempoValuesSortedArray, new ValueChange<Tempo>(time, new Tempo(1)));
            nextTempoChangeIndex = (nextTempoChangeIndex < 0) ? ~nextTempoChangeIndex : nextTempoChangeIndex;
            if (nextTempoChangeIndex == 0)
                return new MetricTimeSpan(RoundMicroseconds(GetMicroseconds(time, Tempo.Default, ticksPerQuarterNote)));
            var tempoChanges = new ArraySegment<ValueChange<Tempo>>(tempoValuesSortedArray, 0, nextTempoChangeIndex);

            //

            var accumulatedMicroseconds = 0d;
            var lastTime = 0L;
            var lastTempo = Tempo.Default;

            foreach (var tempoChange in tempoChanges.Concat(new[] { new ValueChange<Tempo>(time, tempoLine.AtTime(time)) }))
            {
                var tempoChangeTime = tempoChange.Time;

                accumulatedMicroseconds += GetMicroseconds(tempoChangeTime - lastTime, lastTempo, ticksPerQuarterNote);
                lastTempo = tempoChange.Value;
                lastTime = tempoChangeTime;
            }

            return new MetricTimeSpan(RoundMicroseconds(accumulatedMicroseconds));
        }

        private static long MetricTimeSpanToTicks(MetricTimeSpan time, short ticksPerQuarterNote, TempoMap tempoMap)
        {
            var timeMicroseconds = time.TotalMicroseconds;
            if (timeMicroseconds == 0)
                return 0;

            var accumulatedMicroseconds = 0d;
            var lastTime = 0L;
            var lastTempo = Tempo.Default;

            foreach (var tempoChange in tempoMap.Tempo.Values)
            {
                var tempoChangeTime = tempoChange.Time;

                var microseconds = GetMicroseconds(tempoChangeTime - lastTime, lastTempo, ticksPerQuarterNote);
                if (IsGreaterOrEqual(accumulatedMicroseconds + microseconds, timeMicroseconds))
                    break;

                accumulatedMicroseconds += microseconds;
                lastTempo = tempoChange.Value;
                lastTime = tempoChangeTime;
            }

            return RoundMicroseconds(lastTime + (timeMicroseconds - accumulatedMicroseconds) * ticksPerQuarterNote / lastTempo.MicrosecondsPerQuarterNote);
        }

        private static double GetMicroseconds(long time, Tempo tempo, short ticksPerQuarterNote)
        {
            return time * tempo.MicrosecondsPerQuarterNote / (double)ticksPerQuarterNote;
        }

        private static long RoundMicroseconds(double microseconds)
        {
            return (long)Math.Round(microseconds, MidpointRounding.AwayFromZero);
        }

        private static bool IsGreaterOrEqual(double value, long reference)
        {
            const double epsilon = 0.001;
            return value > reference || Math.Abs(value - reference) <= epsilon;
        }

        #endregion
    }
}
