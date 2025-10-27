using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsolePhone
{
    internal class DialerToText
    {
        // Static mapping from digit to corresponding letters on a phone dialer
        private static readonly Dictionary<char, string> DialerTable = new()
            {
                { '1', " " },
                { '2', "abc" },
                { '3', "def" },
                { '4', "ghi" },
                { '5', "jkl" },
                { '6', "mno" },
                { '7', "pqrs" },
                { '8', "tuv" },
                { '9', "wxyz" },
                { '0', "+" }
            };

        private static string GetLetters(char digit)
        {
            return DialerTable.TryGetValue(digit, out var letters) ? letters : string.Empty;
        }

        public static List<String> GetCombinations(string phoneNumber)
        {
            return GetStringCombinations(phoneNumber);
        }

        private static List<String> GetStringCombinations(string phoneNumber)
        {
            List<String> letters = new List<string>();
            // Convert each digit to its corresponding group of letters
            foreach (char digit in phoneNumber)
            {
                string digitletters = GetLetters(digit);
                letters.Add(digitletters);
            }

            // Generate all possible combinations of the letters
            // This is an example of a non-recursive method call.
            //return GetStringCombinations(letters);
            //This is an example of a recursive method call.
            return GetStringCombinationsRecursive(letters);
        }

        /// <summary>
        /// Non-Recursive method to get string combinations.  This gets a list of String combinations 
        /// using 1 character in each String item in the List.
        /// </summary>
        /// <param name="letters"></param>
        /// <returns>All possible string combinations</returns>
        private static List<String> GetStringCombinations(List<String> letters)
        {
            // Initialize a list to hold the new combinations
            List<String> combinations = new List<string>();
            
            for (int i = 0; i < letters.Count; i++)
            {
                bool isFirstRound = true;
                // Temporary list to hold the current combinations
                List<String> subCombinations = new List<string>(combinations);
                foreach (var letter in letters[i])
                {
                    if (i == 0)
                    {
                        combinations.Add(letter.ToString());
                    }
                    else
                    {
                        for (int j = 0; j < subCombinations.Count; j++)
                        {
                            // Append the new letter to each existing combination
                            if (isFirstRound)
                                combinations[j] = subCombinations[j] + letter.ToString();
                            else
                                combinations.Add(subCombinations[j] + letter);
                        }
                        isFirstRound = false;
                    }
                }
            }
            return combinations;
        }

        /// <summary>
        /// Recursive method to get string combinations.  This gets a list of String combinations 
        /// using 1 character in each String item in the List.
        /// </summary>
        /// <param name="letters"></param>
        /// <returns>All possible string combinations</returns>
        private static List<String> GetStringCombinationsRecursive(List<String> letters)
        {
            List<String> combinations = new List<string>();
            if (letters.Count == 0)
            {
                return combinations;
            }
            if (letters.Count == 1)
            {
                foreach (var letter in letters[0])
                {
                    combinations.Add(letter.ToString());
                }
                return combinations;
            }
            // Recursive case: get combinations for the rest of the list
            List<String> subCombinations = GetStringCombinationsRecursive(letters.Skip(1).ToList());
            // Combine the first element with each of the sub-combinations
            foreach (var letter in letters[0])
            {
                foreach (var subCombination in subCombinations)
                {
                    combinations.Add(letter + subCombination);
                }
            }
            return combinations;
        }
    }
}
