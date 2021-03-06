﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Models.Client;


namespace KomaruBot
{
    public class Command
    {
        public static List<Command> commands = new List<Command>();

        public static Command GetCommand(string commandText)
        {
            foreach (var command in commands)
            {
                if (command.commandText != null &&

                    // TODO: should we use .Trim() to compare here? want to avoid commands clashing like cmd and cmd1
                    commandText.StartsWith(command.commandText))
                {
                    return command;
                }
            }

            foreach (var command in HypeCommand.hypeCommands)
            {
                foreach (var cmdText in command.CommandText)
                {
                    if (commandText.Trim() == (cmdText))
                    {
                        return command;
                    }
                }
            }

            // special command for !xxxx guesses
            if (commandText.Length == 5 && commandText.StartsWith("!"))
            {
                var str = commandText.Remove(0, 1);
                if (str.Length == 4 && !str.Any(x => !Char.IsDigit(x))) {
                    var guessCmd = commands.FirstOrDefault(x => x.commandType == Constants.CommandType.Guess);
                    return guessCmd;
                }
            }

            return null;
        }


        public Constants.CommandType commandType { get; private set; }
        public string commandText { get; private set; }
        public Action<ChatMessage, Command> onRun { get; private set; }
        public Constants.AccessLevel requiredAccessLevel { get; private set; }
        public bool requiredRoundStarted { get; private set; }
        public bool requiredRoundNotStarted { get; private set; }
        public Command(
            Constants.CommandType commandType,
            Action<ChatMessage, Command> onRun,
            Constants.AccessLevel requiredAccessLevel,
            bool requiredRoundStarted,
            bool requiredRoundNotStarted,
            string commandText = null)
        {
            this.commandType = commandType;
            this.onRun = onRun;
            this.requiredAccessLevel = requiredAccessLevel;
            this.requiredRoundStarted = requiredRoundStarted;
            this.requiredRoundNotStarted = requiredRoundNotStarted;
            this.commandText = commandText;
            commands.Add(this);
        }
    }
}
