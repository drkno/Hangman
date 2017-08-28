﻿/*
 * Knox.Options
 * This is a Mono.Options semi-compatible library for managing CLI
 * arguments and displaying help text for a program. Created as
 * Mono.Options has an issue and was requiring significant
 * modification to meet my needs. It was quicker to write a new
 * version that supported a similar API than to fix the origional.
 * 
 * Copyright © Matthew Knox, Knox Enterprises 2014-Present.
 * This code is avalible under the MIT license in the state
 * that it was avalible on 05/11/2014 from
 * http://opensource.org/licenses/MIT .
*/

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#endregion

namespace AsyncCombinator
{
    /// <summary>
    /// Set of CLI interface
    /// </summary>
    public class OptionSet : IEnumerable
    {
        /// <summary>
        /// Index of individual options in option set.
        /// </summary>
        private readonly Dictionary<string, int> _lookupDictionary = new Dictionary<string, int>();
        /// <summary>
        /// List of options contained in this option set.
        /// </summary>
        private readonly List<Option> _options = new List<Option>();

        private static readonly char[] ArgumentSeparators = { '=', ':' };

        private static bool _optionStyleCanChange = true;
        private static OptionStyle _optionsStyle = OptionStyle.Nix;
        public static OptionStyle OptionsStyle
        {
            get => _optionsStyle;
            set
            {
                if (!_optionStyleCanChange)
                {
                    throw new Exception("After an option has been added the options style cannot be changed.");
                }
                _optionsStyle = value;
            }
        }
        /// <summary>
        /// Option prefixes for use with various option styles.
        /// </summary>
        private static readonly string[] OptionsPrefixes = { "-", "--", "/", "/" };

        /// <summary>
        /// Enumerator of this OptionSet
        /// </summary>
        /// <returns>The enumerator</returns>
        public IEnumerator GetEnumerator()
        {
            return _options.GetEnumerator();
        }

        /// <summary>
        /// Add a cli option to the set.
        /// </summary>
        /// <param name="cliOptions">The options to associate this option with.</param>
        /// <param name="description">Description of this option.</param>
        /// <param name="func">Action to run when this option is specified.</param>
        /// <param name="conflictSilent">If a cli option has already been specified by a previous option
        /// handle the error silently rather than throwing an exception.</param>
        public void Add(string cliOptions, string description, Action<string> func, bool conflictSilent = true)
        {
            var option = new Option(cliOptions, description, func, OptionsStyle);
            _options.Add(option);
            var ind = _options.Count - 1;
            foreach (var opt in option.Arguments)
            {
                try
                {
                    _lookupDictionary.Add(opt, ind);    // add reference for quick lookup
                }
                catch (Exception e)
                {
                    if (conflictSilent)
                    {
                        continue;
                    }
                    var opt1 = opt;     // remove all instances of this option, as we want to have a good options state
                    foreach (var op in option.Arguments.TakeWhile(op => op != opt1))
                    {
                        _lookupDictionary.Remove(op);
                    }
                    _options.Remove(option);
                    throw new OptionException("Option " + opt + " already specified for another option.", e, opt);
                }
            }
            _optionStyleCanChange = false;
        }

        /// <summary>
        /// Parses a set of arguments into the option equivilents and calls
        /// the actions of those options.
        /// </summary>
        /// <param name="arguments">Arguments to parse.</param>
        /// <returns>List of arguments that parsing failed for.</returns>
        public List<string> Parse(IEnumerable<string> arguments)
        {
            var optionsInError = new List<string>();
            var temp = new List<string>();
            var readForOption = false;
            var optionRead = -1;

            var enumerable = arguments.ToList();

            for (var i = 0; i <= enumerable.Count; i++)
            {
                if (i == enumerable.Count || enumerable[i].StartsWith(OptionsPrefixes[(int)OptionsStyle]) || !readForOption)
                {
                    if (readForOption)
                    {
                        try
                        {
                            var arg = temp.Aggregate("", (current, t) => current + (t + " "));
                            arg = arg.Trim();
                            if (arg.Length == 0 && _options[optionRead].ExpectsArguments)
                            {
                                throw new OptionException("Option expects arguments and none provided.", arg);
                            }
                            _options[optionRead].Action(arg);
                        }
                        catch (OptionException)
                        {
                            optionsInError.Add(enumerable[i - 1 - temp.Count]);
                            optionsInError.AddRange(temp);
                        }
                        finally
                        {
                            temp.Clear();
                        }
                    }

                    if (i == enumerable.Count)
                    {
                        continue;
                    }

                    try
                    {
                        var i1 = i;
                        foreach (var separator in ArgumentSeparators.Where(separator => enumerable[i1].Contains(separator)))
                        {
                            enumerable.RemoveAt(i);
                            enumerable.InsertRange(i, enumerable[i].Split(separator));
                            break;
                        }

                        var ind = _lookupDictionary[enumerable[i]];
                        optionRead = ind;
                        readForOption = true;
                    }
                    catch (Exception)
                    {
                        optionsInError.Add(enumerable[i]);
                        readForOption = false;
                    }
                }
                else
                {
                    temp.Add(enumerable[i]);
                }
            }
            return optionsInError;
        }

        /// <summary>
        /// Parses a set of arguments into the option equivilents and calls
        /// the actions of those options.
        /// </summary>
        /// <param name="arguments">Arguments to parse.</param>
        /// <exception cref="OptionException">On invalid options.</exception>
        public void ParseExceptionally(IEnumerable<string> arguments)
        {
            var result = Parse(arguments);
            if (result.Count <= 0) return;
            var options = "";
            options = result.Aggregate(options, (current, r) => current + (" " + r));
            throw new OptionException("Unknown option" + (result.Count > 1 ? "s" : "") + " " + options, result.ToArray());
        }

        /// <summary>
        /// Style of options to use.
        /// </summary>
        public enum OptionStyle
        {
            Nix = 0,
            Linux = Nix,
            Unix = Nix,
            Osx = Nix,
            Windows = 2
        }

        /// <summary>
        /// Represents an individual option of an OptionSet
        /// </summary>
        private class Option
        {
            /// <summary>
            /// Creates a new option.
            /// </summary>
            /// <param name="options">Cli arguments that use this option.</param>
            /// <param name="description">Description of this option.</param>
            /// <param name="func">Action to perform when this option is specified.</param>
            /// <param name="style">Style of option to use.</param>
            /// <param name="optionalArgs">If this is true, arguments will be treated as non compulsary ones.</param>
            public Option(string options, string description, Action<string> func, OptionStyle style, bool optionalArgs = false)
            {
                Action = func;
                var spl = options.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                Arguments = spl.Select(s => OptionsPrefixes[(int)style + ((s.Length == 1) ? 0 : 1)] + s).ToArray();
                var opts = new List<string>();
                foreach (Match match in Regex.Matches(description, "{[^}]*}"))
                {
                    var val = match.Value.Substring(1, match.Length - 2);
                    opts.Add(val.ToUpper());
                    description = description.Substring(0, match.Index) + val +
                                  description.Substring(match.Index + match.Length);
                }
                Description = description;
                Options = opts.ToArray();
                ExpectsArguments = opts.Count > 0 && !optionalArgs;
            }

            /// <summary>
            /// Arguments that this option provides.
            /// </summary>
            public string[] Arguments { get; }
            /// <summary>
            /// Words to be displayed as options.
            /// </summary>
            public string[] Options { get; }
            /// <summary>
            /// Specifies if this option requires arguments to be passed to it.
            /// </summary>
            public bool ExpectsArguments { get; }
            /// <summary>
            /// Description of this option.
            /// </summary>
            public string Description { get; }
            /// <summary>
            /// Action to perform when this option is specified.
            /// </summary>
            public Action<string> Action { get; }
        }

        #region Help Text

        /// <summary>
        /// Print help.
        /// </summary>
        /// <param name="programNameDescription">Decription to accompany the program name.</param>
        /// <param name="programSynopsis">Synopsis section of the help.</param>
        /// <param name="programAuthor">Author section of the help.</param>
        /// <param name="programReportBugs">Bugs section of the help.</param>
        /// <param name="programCopyright">Copyright section of the help.</param>
        /// <param name="confirm">Halt before continuing execution after printing.</param>
        /// <param name="writer">The writer to write using.</param>
        public void ShowHelp(string programNameDescription,
            string programSynopsis,
            string programAuthor,
            string programReportBugs,
            string programCopyright,
            bool confirm,
            TextWriter writer)
        {
            var textWriter = writer ?? Console.Out;

            WriteProgramName(programNameDescription, ref textWriter);
            WriteProgramSynopsis(programSynopsis, ref textWriter);
            WriteOptionDescriptions(this, ref textWriter);
            WriteProgramAuthor(programAuthor, ref textWriter);
            WriteProgramReportingBugs(programReportBugs, ref textWriter);
            WriteProgramCopyrightLicense(programCopyright, ref textWriter);
        }

        /// <summary>
        /// Print program name and description.
        /// </summary>
        /// <param name="description">Description to print.</param>
        /// <param name="writer">The writer to write using.</param>
        private static void WriteProgramName(string description, ref TextWriter writer)
        {
            var appName = AppDomain.CurrentDomain.FriendlyName;
            writer.WriteLine("NAME");
            writer.WriteLine('\t' + appName + " - " + description + '\n');
        }

        /// <summary>
        /// Print the program synopsis.
        /// </summary>
        /// <param name="synopsis">Synopsis to print.</param>
        /// <param name="writer">The writer to write using.</param>
        private static void WriteProgramSynopsis(string synopsis, ref TextWriter writer)
        {
            var appName = AppDomain.CurrentDomain.FriendlyName;
            writer.WriteLine("SYNOPSIS");
            synopsis = synopsis.Replace("{appName}", appName);
            writer.WriteLine('\t' + synopsis + '\n');
        }

        /// <summary>
        /// Print the program author.
        /// </summary>
        /// <param name="authorByString">Author string to print.</param>
        /// <param name="writer">The writer to write using.</param>
        private static void WriteProgramAuthor(string authorByString, ref TextWriter writer)
        {
            var appName = AppDomain.CurrentDomain.FriendlyName;
            writer.WriteLine("AUTHOR");
            authorByString = authorByString.Replace("{appName}", appName);
            writer.WriteLine('\t' + authorByString + '\n');
        }

        /// <summary>
        /// Print the program reporting bugs section.
        /// </summary>
        /// <param name="reportString">Report bugs string.</param>
        /// <param name="writer">The writer to write using.</param>
        private static void WriteProgramReportingBugs(string reportString, ref TextWriter writer)
        {
            var appName = AppDomain.CurrentDomain.FriendlyName;
            writer.WriteLine("REPORTING BUGS");
            reportString = reportString.Replace("{appName}", appName);
            var spl = reportString.Split(new[] { "\n", "\r\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in spl)
            {
                writer.WriteLine('\t' + s);
            }
            writer.WriteLine();
        }

        /// <summary>
        /// Print the program copyright license.
        /// </summary>
        /// <param name="copyrightLicense">Copyright license text.</param>
        /// <param name="writer">The writer to write using.</param>
        private static void WriteProgramCopyrightLicense(string copyrightLicense, ref TextWriter writer)
        {
            var appName = AppDomain.CurrentDomain.FriendlyName;
            writer.WriteLine("COPYRIGHT");
            copyrightLicense = copyrightLicense.Replace("{appName}", appName);
            var spl = copyrightLicense.Split(new[] { "\n", "\r\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in spl)
            {
                writer.WriteLine('\t' + s);
            }
            writer.WriteLine();
        }

        /// <summary>
        /// Prints all the options in an OptionsSet and prefix/postfix text for the description.
        /// </summary>
        /// <param name="os">OptionsSet to use options from.</param>
        /// <param name="writer">The writer to write using.</param>
        private static void WriteOptionDescriptions(OptionSet os, ref TextWriter writer)
        {
            writer.WriteLine("DESCRIPTION");
            var buffWid = writer == null ? Console.BufferWidth : 120;
            foreach (var p in os._options)
            {
                writer.Write('\t');
                for (var j = 0; j < p.Arguments.Length; j++)
                {
                    writer.Write(p.Arguments[j]);
                    if (j + 1 != p.Arguments.Length)
                    {
                        writer.Write(", ");
                    }
                    else
                    {
                        if (p.Options.Length > 0)
                        {
                            writer.Write('\t');
                            foreach (var t in p.Options)
                            {
                                writer.Write(" [" + t + "]");
                            }
                        }

                        writer.WriteLine();
                    }
                }

                writer.Write("\t\t");
                var len = buffWid - (writer == null ? Console.CursorLeft : 0);

                foreach (var l in p.Description.Split(new[] { "\n", "\r\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var lenP = 0;
                    foreach (var w in l.Split(' '))
                    {
                        var word = w;

                        if (lenP != 0 && (lenP + word.Length + 1) > len)
                        {
                            if (lenP != len) writer.Write("\n");
                            writer.Write("\t\t");
                            lenP = 0;
                        }
                        else if (lenP != 0)
                        {
                            word = ' ' + word;
                        }
                        writer.Write(word);
                        lenP += word.Length;
                    }
                    if (lenP != len) writer.Write("\n");
                    writer.Write("\t\t");
                }
                writer.WriteLine();
            }
            writer.WriteLine();
        }
        #endregion
    }

    /// <summary>
    /// Exception that is thrown when there is an error with the options specified.
    /// </summary>
    [Serializable]
    public class OptionException : Exception
    {
        /// <summary>
        /// Create a new OptionException.
        /// </summary>
        /// <param name="errorText">The description of this exception.</param>
        /// <param name="errorArguments">Arguments that were in error.</param>
        public OptionException(string errorText, params string[] errorArguments) : base(errorText)
        {
            ErrorArguments = errorArguments;
        }

        /// <summary>
        /// Create a new OptionException.
        /// </summary>
        /// <param name="errorText">The description of this exception.</param>
        /// <param name="innerException">The inner exception that caused this one to occur.</param>
        /// <param name="errorArguments">Arguments that were in error.</param>
        public OptionException(string errorText, Exception innerException, params string[] errorArguments)
            : base(errorText, innerException)
        {
            ErrorArguments = errorArguments;
        }

        /// <summary>
        /// Arguments that were in error.
        /// </summary>
        public string[] ErrorArguments { get; }
    }
}