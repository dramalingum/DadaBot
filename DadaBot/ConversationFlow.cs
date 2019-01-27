namespace DadaBot
{
    public class ConversationFlow
    {
        public enum Question
        {
            Name,
            Age,
            Date,
            None, // Our last action did not involve a question.
        }

        // The last question asked.
        public Question LastQuestionAsked { get; set; } = Question.None;
    }
}
