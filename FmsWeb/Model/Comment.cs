using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FmsWeb.Model
{
    public class Comment
    {
        public int Id { get; set; }
        public string Alias { get; set; }
        public string TransactionId { get; set; }
        public string CommentText { get; set; }
        public DateTime DateCreated { get; set; }
    }
}