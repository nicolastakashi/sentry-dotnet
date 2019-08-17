using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace Sentry.PlatformAbstractions
{
    // https://github.com/dotnet/corefx/issues/17452
    internal static class RuntimeInfo
    {
        private static readonly Regex RuntimeParseRegex = new Regex("^(?<name>[^\\d]*)(?<version>(\\d+\\.)+[^\\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Gets the current runtime.
        /// </summary>
        /// <returns>A new instance for the current runtime</returns>
        internal static Runtime GetRuntime()
        {
            var runtime = GetFromRuntimeInformation();

#if NETFX
            SetReleaseAndVersionNetFx(runtime);
#elif NETSTANDARD || NETCOREAPP // Possibly .NET Core
            SetNetCoreVersion(runtime);
#endif
            return runtime;
        }

        internal static Runtime Parse(string rawRuntimeDescription, string name = null)
        {
            if (rawRuntimeDescription == null)
            {
                return name == null
                    ? null
                    : new Runtime(name);
            }

            var match = RuntimeParseRegex.Match(rawRuntimeDescription);
            if (match.Success)
            {
                return new Runtime(
                    name ?? (match.Groups["name"].Value == string.Empty
                        ? null
                        : match.Groups["name"].Value.Trim()),
                    match.Groups["version"].Value,
                    raw: rawRuntimeDescription);
            }

            return new Runtime(name, raw: rawRuntimeDescription);
        }

#if NETFX
        internal static void SetReleaseAndVersionNetFx(Runtime runtime)
        {
            if (runtime?.IsNetFx() == true)
            {
                var latest = FrameworkInfo.GetLatest(Environment.Version.Major);

                runtime.FrameworkInstallation = latest;
                if (latest.Version?.Major < 4)
                {
                    // prior to 4, user-friendly versions are always 2 digit: 1.0, 1.1, 2.0, 3.0, 3.5
                    runtime.Version = latest.ServicePack == null
                        ? $"{latest.Version.Major}.{latest.Version.Minor}"
                        : $"{latest.Version.Major}.{latest.Version.Minor} SP {latest.ServicePack}";
                }
                else
                {
                    runtime.Version = latest.Version?.ToString();
                }
            }
        }
#endif

#if NETSTANDARD || NETCOREAPP // Possibly .NET Core
        // Known issue on Docker: https://github.com/dotnet/BenchmarkDotNet/issues/448#issuecomment-361027977
        internal static void SetNetCoreVersion(Runtime runtime)
        {
            if (runtime?.IsNetCore() == true)
            {
                // https://github.com/dotnet/BenchmarkDotNet/issues/448#issuecomment-308424100
                var assembly = typeof(System.Runtime.GCSettings).Assembly;
                var assemblyPath = assembly.CodeBase.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
                if (netCoreAppIndex > 0
                    && netCoreAppIndex < assemblyPath.Length - 2)
                {
                    runtime.Version = assemblyPath[netCoreAppIndex + 1];
                }
            }
        }
#endif

        internal static Runtime GetFromRuntimeInformation()
        {
            // Prefered API: netstandard2.0
            // https://github.com/dotnet/corefx/blob/master/src/System.Runtime.InteropServices.RuntimeInformation/src/System/Runtime/InteropServices/RuntimeInformation/RuntimeInformation.cs
            // https://github.com/mono/mono/blob/90b49aa3aebb594e0409341f9dca63b74f9df52e/mcs/class/corlib/System.Runtime.InteropServices.RuntimeInformation/RuntimeInformation.cs
            // e.g: .NET Framework 4.7.2633.0, .NET Native, WebAssembly
            var frameworkDescription = RuntimeInformation.FrameworkDescription;

            return Parse(frameworkDescription);
        }
    }
}
