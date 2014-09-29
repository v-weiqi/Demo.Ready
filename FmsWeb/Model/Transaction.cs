using System;

namespace FmsWeb.Model
{
    public class Transaction
    {
        public Transaction(DateTime transactionDate, string description)
        {
            TransactionDate = transactionDate;
            var oldString = "Submission State\n    old: ";
            var newString = "\n    new: ";

            var oldStateStartIndex = oldString.Length-1;
            var oldStateEndIndex = description.IndexOf(newString);

            var newStateStartIndex = description.IndexOf(newString) + newString.Length;

            var newState = description.Substring(newStateStartIndex).Replace(" ","");
            var oldState = description.Substring(oldStateStartIndex, oldStateEndIndex - oldStateStartIndex).Replace(" ", "");

            Old = (TransactionState)Enum.Parse(typeof(TransactionState), oldState);
            New = (TransactionState)Enum.Parse(typeof(TransactionState), newState);



        }

        public DateTime TransactionDate { get; set; }
        public TransactionState Old { get; set; }
        public TransactionState New { get; set; }

        public override string  ToString()
        {
            return TransactionDate.ToString();
        }
    }
}