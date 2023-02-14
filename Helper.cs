using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace aicommits;
internal class Helper
{
    public static string GetPrompt()
    {
        return @"[bold white]Commit message: {0}\n[/]";
    }
}
