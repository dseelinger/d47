namespace D47.Core.Interface;

public static class InterfaceForms
{
    extension(PanelNavigator nav)
    {
        public bool JumpTo(string word)
        {
            var trail = nav.Trail;

            for (var index = trail.Count - 2; index >= 0; index--)
            {
                if (string.Equals(trail[index].Word, word, StringComparison.OrdinalIgnoreCase))
                {
                    return nav.JumpTo(index);
                }
            }

            return false;
        }
    }
}
