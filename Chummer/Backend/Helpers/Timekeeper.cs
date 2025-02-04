/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Chummer
{
    public static class Timekeeper
    {
        private static readonly Lazy<Logger> s_ObjLogger = new Lazy<Logger>(LogManager.GetCurrentClassLogger);
        private static Logger Log => s_ObjLogger.Value;
        private static readonly Stopwatch s_Time = new Stopwatch();
        private static readonly LockingDictionary<string, TimeSpan> s_DictionaryStarts = new LockingDictionary<string, TimeSpan>();
        private static readonly LockingDictionary<string, Tuple<TimeSpan, int>> s_DictionaryStatistics = new LockingDictionary<string, Tuple<TimeSpan, int>>();

        static Timekeeper()
        {
            s_Time.Start();
        }

        [CLSCompliant(false)]
        public static CustomActivity StartSyncron(string taskname, CustomActivity parentActivity, CustomActivity.OperationType operationType, string target)
        {
            CustomActivity dependencyActivity = new CustomActivity(taskname, parentActivity, operationType, target);
            s_DictionaryStarts.TryAdd(taskname, s_Time.Elapsed);
            return dependencyActivity;
        }

        [CLSCompliant(false)]
        public static CustomActivity StartSyncron(string taskname, CustomActivity parentActivity)
        {
            CustomActivity dependencyActivity = new CustomActivity(taskname, parentActivity);
            s_DictionaryStarts.TryAdd(taskname, s_Time.Elapsed);
            return dependencyActivity;
        }

        [CLSCompliant(false)]
        public static async ValueTask<CustomActivity> StartSyncronAsync(string taskname, CustomActivity parentActivity, CustomActivity.OperationType operationType, string target, CancellationToken token = default)
        {
            CustomActivity dependencyActivity = new CustomActivity(taskname, parentActivity, operationType, target);
            await s_DictionaryStarts.TryAddAsync(taskname, s_Time.Elapsed, token).ConfigureAwait(false);
            return dependencyActivity;
        }

        [CLSCompliant(false)]
        public static async ValueTask<CustomActivity> StartSyncronAsync(string taskname, CustomActivity parentActivity, CancellationToken token = default)
        {
            CustomActivity dependencyActivity = new CustomActivity(taskname, parentActivity);
            await s_DictionaryStarts.TryAddAsync(taskname, s_Time.Elapsed, token).ConfigureAwait(false);
            return dependencyActivity;
        }

        public static TimeSpan Elapsed(string taskname)
        {
            if (s_DictionaryStarts.TryGetValue(taskname, out TimeSpan objStartTimeSpan))
            {
                return s_Time.Elapsed - objStartTimeSpan;
            }

            return TimeSpan.Zero;
        }

        public static async ValueTask<TimeSpan> ElapsedAsync(string taskname, CancellationToken token = default)
        {
            (bool blnSuccess, TimeSpan objStartTimeSpan) = await s_DictionaryStarts.TryGetValueAsync(taskname, token).ConfigureAwait(false);
            if (blnSuccess)
            {
                return s_Time.Elapsed - objStartTimeSpan;
            }

            return TimeSpan.Zero;
        }

        public static TimeSpan Finish(string taskname)
        {
            TimeSpan final = TimeSpan.Zero;
            if (s_DictionaryStarts.TryRemove(taskname, out TimeSpan objStartTimeSpan))
            {
                final = s_Time.Elapsed - objStartTimeSpan;

#if DEBUG
                string strLogEntry = string.Format(GlobalSettings.InvariantCultureInfo, "Task \"{0}\" finished in {1}",
                    taskname, final);
                //Log.Trace(strLogEntry);

                Debug.WriteLine(strLogEntry);
#endif

                s_DictionaryStatistics.AddOrUpdate(taskname, x => new Tuple<TimeSpan, int>(final, 1),
                                                   (x, y) => new Tuple<TimeSpan, int>(
                                                       y.Item1 + final, y.Item2 + 1));
            }
            else
            {
                Debug.WriteLine("Non started task \"" + taskname + "\" finished");
            }
            return final;
        }

        public static async ValueTask<TimeSpan> FinishAsync(string taskname, CancellationToken token = default)
        {
            TimeSpan final = TimeSpan.Zero;
            (bool blnSuccess, TimeSpan objStartTimeSpan) = await s_DictionaryStarts.TryRemoveAsync(taskname, token).ConfigureAwait(false);
            if (blnSuccess)
            {
                final = s_Time.Elapsed - objStartTimeSpan;

#if DEBUG
                string strLogEntry = string.Format(GlobalSettings.InvariantCultureInfo, "Task \"{0}\" finished in {1}",
                                                   taskname, final);
                //Log.Trace(strLogEntry);

                Debug.WriteLine(strLogEntry);
#endif

                await s_DictionaryStatistics.AddOrUpdateAsync(taskname, x => new Tuple<TimeSpan, int>(final, 1),
                                                              (x, y) => new Tuple<TimeSpan, int>(
                                                                  y.Item1 + final, y.Item2 + 1), token).ConfigureAwait(false);
            }
            else
            {
                Debug.WriteLine("Non started task \"" + taskname + "\" finished");
            }
            return final;
        }

        public static void MakeLog()
        {
            string strLog;
            using (new FetchSafelyFromPool<StringBuilder>(Utils.StringBuilderPool,
                                                          out StringBuilder sbdLog))
            {
                sbdLog.AppendLine("Time statistics");
                foreach (KeyValuePair<string, Tuple<TimeSpan, int>> keyValuePair in s_DictionaryStatistics)
                {
                    sbdLog.AppendFormat(GlobalSettings.InvariantCultureInfo, "\t{0}({1}) = {2}{3}",
                                        keyValuePair.Key, keyValuePair.Value.Item2, keyValuePair.Value.Item1,
                                        Environment.NewLine);
                }

                strLog = sbdLog.ToString();
            }

            Debug.WriteLine(strLog);
            Log.Info(strLog);
        }
    }
}
