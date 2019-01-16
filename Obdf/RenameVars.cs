using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

//Импортируем ещё 5 необходимых пространства имён
using System.Windows.Forms;
using EnvDTE;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Obdf
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class RenameVars
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("53348cdf-603d-4a0b-87d5-1253e2a6b4f9");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenameVars"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private RenameVars(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RenameVars Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in RenameVars's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new RenameVars(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            TextDocument activeDoc = dte.ActiveDocument.Object() as TextDocument;

            var text = activeDoc.CreateEditPoint(activeDoc.StartPoint).GetText(activeDoc.EndPoint);

            HashSet<string> keywords = new HashSet<string>{"alignas", "alignof", "and", "and_eq", "asm", "atomic_cancel", "atomic_commit", "atomic_noexcept", "auto", "bitand", "bitor", "bool", "break", "case", "catch",
            "char", "char16_t", "char32_t", "class", "compl", "concept", "const", "constexpr", "const_cast", "continue", "co_await", "co_return", "co_yield", "decltype", "default", "delete",
            "do", "double", "dynamic_cast", "else", "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "0;}", "if", "import","include", "inline", "int", "long", "main", "module", "mutable",
            "namespace", "new", "noexcept", "not", "not_eq", "nullptr", "operator", "or", "or_eq", "private", "protected", "public", "register", "reinterpret_cast", "requires", "return", "short",
            "signed", "sizeof", "static", "string", "std", "static_assert", "static_cast", "struct", "switch", "synchronized", "template", "this", "thread_local", "throw", "true", "try", "typedef", "typeid",
            "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq" };

            Dictionary<string, string> words = new Dictionary<string, string>();
            Regex cut = new Regex(".*[ \t\n]+");

            Regex variableNames = new Regex(@"(?:[a-zA-Z0-9_\<\>]+|,+)[ \t]+[a-zA-Z_]+[a-zA-Z0-9_]*(?=[ \t]*\=.*;|[ \t]*;|[ \t]*,|[ \t]*\(|[ \t]*:)");

            MatchCollection matches = variableNames.Matches(text);

            foreach (Match match in matches)
            {
                string variableName = cut.Replace(match.Value.ToString(), "");
                if (!keywords.Contains(variableName))
                {
                    words[variableName] = "a"+(Math.Abs(variableName.GetHashCode())).ToString();
                }
            }

            foreach (var word in words)
            {
                Regex changeName = new Regex("(?<![a-zA-Z0-9_])"+ word.Key+"(?![a-zA-Z0-9_])");
                text = changeName.Replace(text, word.Value);
            }
            activeDoc.CreateEditPoint(activeDoc.StartPoint).Delete(activeDoc.EndPoint);
            (dte.ActiveDocument.Selection as EnvDTE.TextSelection).Text = text;
        }
    }
}
