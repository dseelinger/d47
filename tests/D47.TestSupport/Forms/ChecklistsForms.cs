namespace D47.Core.Checklists;

public static class ChecklistForms
{
    extension(ChecklistDocument document)
    {
        public ChecklistChange DeleteCompleted() => document.DeleteCompleted(_ => true);
    }
}
