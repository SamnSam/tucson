using System;
using System.Diagnostics;

namespace Tucson
{
	/// <summary>
	/// methods for tucson timings
	/// </summary>
	public static class TucsonTimings
	{
		internal static string GetMethodNameFromStack(int index)
		{
			return new StackFrame(index + 1).GetMethod().Name;
		}

		/// <summary>
		/// Perform a timed operation and log the time it took to Splunk.  The operation name defaults to the calling method (which may disappear in release builds due to optimizations).
		/// </summary>
		/// <typeparam name="T">A return type</typeparam>
		/// <param name="source">the source of the operation (use the type name)</param>
		/// <param name="op">The delegate that performs the operation</param>
		/// <returns>The time it took</returns>
		public static Tuple<T, long> PerformTimedOperation<T>(string source, Func<T> op)
		{
			return PerformTimedOperation(source, GetMethodNameFromStack(1), op);
		}

		/// <summary>
		/// Perform a timed operation and log the time it took to Splunk.  The operation name defaults to the calling method (which may disappear in release builds due to optimizations).
		/// </summary>
		/// <param name="source">the source of the operation (use the type name)</param>
		/// <param name="op">The delegate that performs the operation</param>
		/// <returns>The time it took</returns>
		public static long PerformTimedOperation(string source, Action op)
		{
			return PerformTimedOperation(source, GetMethodNameFromStack(1), op);
		}

		/// <summary>
		/// Perform a timed operation and log the time it took to Splunk
		/// </summary>
		/// <typeparam name="T">A return type</typeparam>
		/// <param name="source">the source of the operation (use the type name)</param>
		/// <param name="opName">The name of the operation to perform</param>
		/// <param name="op">The delegate that performs the operation</param>
		/// <returns>The result of the operation and the time it took</returns>
		public static Tuple<T, long> PerformTimedOperation<T>(string source, string opName, Func<T> op)
		{
			var sw = new Stopwatch();
			sw.Start();

			var ret = op();

			sw.Stop();

			SendToSplunk(source, opName, sw);

			return new Tuple<T, long>(ret, sw.ElapsedMilliseconds);
		}

		/// <summary>
		/// Perform a timed operation and log the time it took to Splunk
		/// </summary>
		/// <param name="source">the source of the operation (use the type name)</param>
		/// <param name="opName">The name of the operation to perform</param>
		/// <param name="op">The delegate that performs the operation</param>
		/// <returns>The time it took</returns>
		public static long PerformTimedOperation(string source, string opName, Action op)
		{
			var ret = PerformTimedOperation(
				source,
				opName,
				() =>
					{
						op();
						return 0;
					});

			return ret.Item2;
		}

		/// <summary>
		/// Sends a message to splunk, including the time.
		/// </summary>
		/// <param name="source">The source</param>
		/// <param name="method"></param>
		/// <param name="timer"></param>
		static public void SendToSplunk(string source, string method, Stopwatch timer)
		{
			var splunkString = "Time=" + DateTime.UtcNow.ToString("o") + " Source=" + source + " Method=" + method + " TimeElapsed=" + Decimal.Divide(timer.Elapsed.Milliseconds, 1000);
			//if you implement Splunk then you can use this - Splunk.Send(splunkString);
		}
	}
}
