namespace D47.Core.Audio;

/// <summary>
/// Whether a sender's name reads as a woman's, so a voice can be chosen to match
/// (remediation.md, "Named NPCs should each use a different voice").
/// <para>
/// <b>Elite records no sex anywhere.</b> 914 journals, 696,000 events, 221 event kinds, and not
/// one carries a gender field — not <c>ReceiveText</c>, not <c>CrewHire</c>, not the passenger
/// manifests. So this cannot be derived from what the game states, and no amount of reading the
/// journals will make it derivable. What the journals do supply is the <em>names</em>, and this
/// is a list of the ones that read as a woman's.
/// </para>
/// <para>
/// <b>A list rather than a rule.</b> Guessing from an ending — an "a", an "ie" — is a different
/// question answered confidently: it makes Joshua and Luca women and leaves Ingrid and Meg men.
/// A closed list is wrong only where it is short, and it gets longer.
/// </para>
/// <para>
/// <b>Unknown means a man’s voice, which is a choice and not an accident.</b> That is what the
/// request asked for, and it is the safe half of the error: a name this does not recognise is
/// far more often a man’s in the material this is used on. Measured over 9,091 named-NPC
/// transmissions across 914 journals, 4.8% match — low, and it reflects what is out there rather
/// than a gap here: of the forty commonest names this does not match, every one is Mark, John,
/// Paul, Andrew, David, Michael, or not a personal name at all.
/// <c>tools/scan-npc-names.py</c> reports what is still unmatched, most heard first, which is how
/// this grows.
/// </para>
/// <para>
/// The names themselves are ordinary given names and are not game data. The <em>set</em> of names
/// worth having came from reading a Commander’s own journals, and the tool that reads them is
/// beside this rather than being something anybody has to reconstruct.
/// </para>
/// </summary>
public static class GivenNames
{
    /// <summary>
    /// Given names that read as a woman’s. Ambiguous ones are deliberately absent — Sam, Alex,
    /// Jo, Pip and Robin are men as often as women in this material, and the default is the one
    /// that is right more often.
    /// </summary>
    private static readonly HashSet<string> Feminine = new(StringComparer.OrdinalIgnoreCase)
    {
        "Abbie", "Abby", "Abigail", "Ada", "Addie", "Adela", "Adele", "Adriana", "Adrienne",
        "Agatha", "Agnes", "Aida", "Aileen", "Aimee", "Aisha", "Alana", "Alba", "Alberta",
        "Alejandra", "Alessandra", "Alexa", "Alexandra", "Alexandria", "Alice", "Alicia",
        "Alina", "Alison", "Allie", "Allison", "Alma", "Alyssa", "Amanda", "Amber", "Amelia",
        "Amy", "Ana", "Anastasia", "Andrea", "Angela", "Angelica", "Angelina", "Angie", "Anita",
        "Ann", "Anna", "Annabel", "Annabelle", "Anne", "Annette", "Annie", "Antonia", "April",
        "Ariana", "Ariel", "Arlene", "Astrid", "Audrey", "Aurora", "Autumn", "Ava", "Barbara",
        "Bea", "Beatrice", "Beatriz", "Bec", "Becca", "Becky", "Belinda", "Bella", "Belle",
        "Bernadette", "Bernice", "Bertha", "Bess", "Beth", "Bethany", "Betsy", "Bette", "Betty",
        "Bev", "Beverly", "Bianca", "Blanca", "Bonnie", "Brenda", "Bridget", "Brigitte",
        "Britney", "Brittany", "Bronwen", "Brooke", "Caitlin", "Camila", "Camille", "Candace",
        "Candice", "Cara", "Carla", "Carly", "Carmen", "Carol", "Carole", "Carolina",
        "Caroline", "Carolyn", "Carrie", "Cass", "Cassandra", "Cassie", "Catalina", "Caterina",
        "Catherine", "Cathi", "Cathy", "Cecilia", "Celeste", "Celia", "Chantal", "Charlene",
        "Charlotte", "Chelsea", "Cherie", "Cheryl", "Chiara", "Chloe", "Christina", "Christine",
        "Christy", "Cindy", "Claire", "Clara", "Clarissa", "Claudia", "Clementine", "Colette",
        "Colleen", "Connie", "Constance", "Cora", "Coral", "Corinne", "Courtney", "Cristina",
        "Crystal", "Cynthia", "Daisy", "Daniela", "Danielle", "Daphne", "Darlene", "Dawn",
        "Deanna", "Debbie", "Deborah", "Debra", "Deirdre", "Delia", "Delilah", "Della",
        "Denise", "Diana", "Diane", "Dianne", "Dina", "Dolores", "Dominique", "Donna", "Dora",
        "Doreen", "Doris", "Dorothy", "Dot", "Dulce", "Edith", "Edna", "Effie", "Eileen",
        "Elaine", "Eleanor", "Elena", "Eliana", "Elin", "Elisa", "Elisabeth", "Elise", "Eliza",
        "Elizabeth", "Ella", "Ellen", "Ellie", "Eloise", "Els", "Elsa", "Elsie", "Elvira",
        "Emilia", "Emily", "Emma", "Emmanuelle", "Enid", "Erica", "Erika", "Erin", "Esme",
        "Esmeralda", "Esperanza", "Estelle", "Esther", "Ethel", "Etta", "Eugenia", "Eunice",
        "Eva", "Evangeline", "Eve", "Evelyn", "Evie", "Faith", "Farah", "Fatima", "Fay", "Faye",
        "Felicia", "Felicity", "Fern", "Fernanda", "Fiona", "Flo", "Flora", "Florence", "Fran",
        "Frances", "Francesca", "Freda", "Freya", "Frida", "Gabriela", "Gabriella", "Gabrielle",
        "Gail", "Gemma", "Genevieve", "Georgia", "Georgina", "Geraldine", "Gertrude", "Gill",
        "Gillian", "Gina", "Ginger", "Gisela", "Giselle", "Gladys", "Glenda", "Gloria", "Grace",
        "Gracie", "Greta", "Gretchen", "Guadalupe", "Gwen", "Gwendolyn", "Hannah", "Harriet",
        "Hattie", "Hazel", "Heather", "Heidi", "Helen", "Helena", "Helene", "Henrietta",
        "Hilary", "Hilda", "Hillary", "Holly", "Hope", "Hyacinth", "Ida", "Ilse", "Imani",
        "Imogen", "Ines", "Inga", "Ingrid", "Irene", "Iris", "Irma", "Isabel", "Isabella",
        "Isabelle", "Isla", "Ivy", "Izzy", "Jacinta", "Jacki", "Jackie", "Jacqueline", "Jacqui",
        "Jade", "Jane", "Janet", "Janice", "Janine", "Jasmine", "Jean", "Jeanette", "Jeanne",
        "Jenna", "Jennifer", "Jenny", "Jessa", "Jessica", "Jessie", "Jewel", "Jill", "Joan",
        "Joanna", "Joanne", "Jocelyn", "Jodie", "Joelle", "Johanna", "Jolene", "Josefina",
        "Josephine", "Josie", "Joy", "Joyce", "Juanita", "Judith", "Judy", "Julia", "Juliana",
        "Julie", "Juliet", "Juliette", "June", "Justine", "Kara", "Karen", "Karin", "Karina",
        "Karla", "Kat", "Kate", "Katherine", "Kathi", "Kathleen", "Kathryn", "Kathy", "Katie",
        "Katrina", "Katya", "Kay", "Kayla", "Kaz", "Keira", "Kelly", "Kelsey", "Kendra",
        "Kerry", "Khadija", "Kiki", "Kimberly", "Kirsten", "Kirsty", "Klara", "Kristen",
        "Kristin", "Kristina", "Krystal", "Kyla", "Lacey", "Laila", "Lana", "Lara", "Larissa",
        "Laura", "Laurel", "Lauren", "Laurie", "Layla", "Lea", "Leah", "Leanne", "Lena",
        "Leona", "Leonora", "Leslie", "Leticia", "Lidia", "Lila", "Lilian", "Lillian", "Lily",
        "Lina", "Linda", "Lindsay", "Lindsey", "Lisa", "Liv", "Livia", "Liz", "Liza", "Lizzie",
        "Lois", "Lola", "Lorena", "Loretta", "Lori", "Lorna", "Lorraine", "Lottie", "Louisa",
        "Louise", "Lucia", "Luciana", "Lucille", "Lucinda", "Lucy", "Ludmila", "Luisa", "Lulu",
        "Lydia", "Lynda", "Lynn", "Lynne", "Mabel", "Madeleine", "Madeline", "Madison", "Mae",
        "Magdalena", "Maggie", "Mags", "Maia", "Maisie", "Malia", "Mandy", "Manon", "Mara",
        "Marcella", "Marcia", "Margaret", "Margarita", "Margot", "Maria", "Mariah", "Mariana",
        "Marianne", "Maribel", "Marie", "Marilyn", "Marina", "Marion", "Marisa", "Marissa",
        "Maritza", "Marjorie", "Marlene", "Marsha", "Marta", "Martha", "Martina", "Mary",
        "Maureen", "Mavis", "Maxine", "Maya", "Meg", "Megan", "Meghan", "Melanie", "Melinda",
        "Melissa", "Mercedes", "Meredith", "Mia", "Michaela", "Michele", "Michelle", "Mila",
        "Mildred", "Millicent", "Millie", "Mimi", "Mina", "Mira", "Miranda", "Miriam", "Misty",
        "Mitzi", "Moira", "Mollie", "Molly", "Mona", "Monica", "Monique", "Muriel", "Myra",
        "Myrtle", "Nadia", "Nadine", "Nancy", "Naomi", "Natalia", "Natalie", "Natasha", "Neda",
        "Nell", "Nellie", "Nessa", "Nettie", "Nicole", "Nikki", "Nina", "Noelle", "Nora",
        "Norah", "Noreen", "Norma", "Octavia", "Odette", "Olga", "Olive", "Olivia", "Opal",
        "Ophelia", "Paige", "Paloma", "Pam", "Pamela", "Patricia", "Patsy", "Patty", "Paula",
        "Paulette", "Pauline", "Pearl", "Peggy", "Penelope", "Penny", "Perla", "Petra",
        "Phoebe", "Phyllis", "Pilar", "Polly", "Poppy", "Priscilla", "Prudence", "Rachel",
        "Rae", "Ramona", "Raquel", "Rebecca", "Regina", "Renata", "Renee", "Rhoda", "Rhonda",
        "Ria", "Rita", "Roberta", "Robyn", "Rochelle", "Rosa", "Rosalie", "Rosalind", "Rose",
        "Rosemary", "Rosie", "Rowena", "Roxana", "Roxanne", "Ruby", "Ruth", "Sabina", "Sabine",
        "Sabrina", "Sadie", "Sally", "Salma", "Samantha", "Sandra", "Sandy", "Sara", "Sarah",
        "Sasha", "Saskia", "Scarlett", "Selena", "Selina", "Selma", "Serena", "Shannon",
        "Sharon", "Sheila", "Shelby", "Shelley", "Sherry", "Shirley", "Sian", "Sibyl", "Silvia",
        "Simone", "Sinead", "Sofia", "Sonia", "Sonja", "Sonya", "Sophia", "Sophie", "Stacey",
        "Stacy", "Stella", "Steph", "Stephanie", "Sue", "Susan", "Susana", "Susanna", "Susanne",
        "Suzanne", "Sybil", "Sylvia", "Tabitha", "Talia", "Tamara", "Tammy", "Tania", "Tanya",
        "Tara", "Tasha", "Tatiana", "Teresa", "Tess", "Tessa", "Thea", "Thelma", "Theresa",
        "Therese", "Tiffany", "Tilly", "Tina", "Tonia", "Tracey", "Trish", "Trudy", "Ulla",
        "Ursula", "Valentina", "Valeria", "Valerie", "Vanessa", "Vera", "Verena", "Verity",
        "Veronica", "Vicki", "Vicky", "Victoria", "Vida", "Viola", "Violet", "Virginia",
        "Vivian", "Viviana", "Vivien", "Vivienne", "Wanda", "Wendy", "Whitney", "Wilhelmina",
        "Willa", "Willow", "Wilma", "Winifred", "Winnie", "Xenia", "Ximena", "Yasmin",
        "Yasmine", "Yolanda", "Yvette", "Yvonne", "Zara", "Zelda", "Zoe", "Zoey", "Zora"
    };

    /// <summary>
    /// Whether this sender should be given a woman’s voice.
    /// <para>
    /// The first word only, which is where a given name is. Elite’s NPC senders are
    /// "Marianne Hobbs", "Jacqui Waugh", "Ray Watkinson" — a given name and a family name — and
    /// a family name is not evidence of anything. A leading quote is stripped because the
    /// generator produces nicknames like <c>'Blaze' Furey</c>.
    /// </para>
    /// <para>
    /// Untrusted input: another Commander or Frontier's generator chose this string. It is only
    /// ever used as a set lookup.
    /// </para>
    /// </summary>
    public static bool ReadsFemale(string? sender)
    {
        if (sender is not { Length: > 0 })
        {
            return false;
        }

        var space = sender.IndexOf(' ');
        var first = (space < 0 ? sender : sender[..space]).Trim('\'', '"', '.', ',');

        return first.Length > 0 && Feminine.Contains(first);
    }
}
