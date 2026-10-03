using System.Diagnostics;
using System.Text;

const string DotNet = @"C:\Users\jack\.dotnet\dotnet.exe";
const string TestProject = @"C:\Users\jack\RiderProjects\FlippingExilePSAPI\tests\PoE.Valuation.UnitTests";
string trx = Path.Combine(Path.GetTempPath(), "tests.trx");
string log = Path.Combine(Path.GetTempPath(), "testdriver.log");

var sb = new StringBuilder();
var psi = new ProcessStartInfo(DotNet)
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    WorkingDirectory = TestProject,
    UseShellExecute = false,
};
psi.ArgumentList.Add("test");
psi.ArgumentList.Add(TestProject);
psi.ArgumentList.Add("--nologo");
psi.ArgumentList.Add($"--logger:trx;LogFilePath={trx}");

using var p = Process.Start(psi)!;
var outTask = p.StandardOutput.ReadToEndAsync();
var errTask = p.StandardError.ReadToEndAsync();
p.WaitForExit(10 * 60 * 1000);
sb.AppendLine($"EXIT: {p.ExitCode}");
sb.AppendLine($"TRX_EXISTS: {File.Exists(trx)}");
sb.AppendLine("---- STDOUT (tail) ----");
sb.AppendLine(Tail(outTask.Result, 4000));
sb.AppendLine("---- STDERR (tail) ----");
sb.AppendLine(Tail(errTask.Result, 4000));
File.WriteAllText(log, sb.ToString());
Console.WriteLine(log);

static string Tail(string s, int n)
{
    s = s ?? "";
    return s.Length <= n ? s : s[^n..];
}
