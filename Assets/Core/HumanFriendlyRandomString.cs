using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtCom.Logging
{
    public static class HumanFriendlyRandomString
    {
        private static readonly string[] AnimalNames = new string[]
        {
            "Alligator", "Alpaca", "Ant", "Antelope", "Ape", "Armadillo",
            "Baboon", "Badger", "Bat", "Bear", "Beaver", "Bee", "Beetle", "Buffalo", "Butterfly",
            "Camel", "Cat", "Cheetah", "Chimpanzee", "Clam", "Crab", "Crow",
            "Deer", "Dinosaur", "Dog", "Dolphin", "Duck",
            "Eel", "Elephant",
            "Ferret", "Fish", "Fly", "Fox", "Frog",
            "Giraffe", "Goat", "Gorilla",
            "Hamster", "Horse", "Hyena",
            "Kangaroo", "Koala",
            "Leopard", "Lion", "Lizard", "Llama",
            "Mammoth", "Mink", "Mole", "Monkey", "Mouse",
            "Otter", "Ox", "Oyster",
            "Panda", "Pig",
            "Rabbit", "Raccoon",
            "Seal", "Shark", "Sheep", "Snake", "Spider", "Squirrel",
            "Tiger", "Turtle",
            "Wasp", "Weasel", "Whale", "Wolf",
            "Yak",
            "Zebra"
        };
        private static readonly string[] Adjectives = new string[]
        {
            "Good", "New", "First", "Last", "Long", "Great", "Little", "Old", "Big", "High", "Different", 
            "Small", "Large", "Young", "Important", "Bad", "Adorable", "Beautiful", "Clean", "Elegant",
            "Fancy", "Glamorous", "Magnificent", "Old-Fashioned", "Plain", "Wide-Eyed", "Red", "Orange",
            "Yellow", "Green", "Blue", "Purple", "Gray", "Black", "White", "Careful", "Clever", "Famous",
            "Gifted", "Helpful", "Important", "Odd", "Powerful", "Shy", "Vast", "Wrong", "Brave", "Calm",
            "Happy", "Kind", "Lively", "Nice", "Silly", "Angry", "Fierce", "Grumpy", "Lazy", "Nervous",
            "Scary", "Obnoxious", "Massive", "Short", "Small"
        };
        
        public static string Create(Random rnd)
        {
            string result = "";
            for (int i = rnd.Next(1, 3); i > 0; i--)
            {
                result = AnimalNames[rnd.Next(0, AnimalNames.Length)] + result;
            }
            for (int i = rnd.Next(1, 3); i > 0; i--)
            {
                result = Adjectives[rnd.Next(0, Adjectives.Length)] + result;
            }
            return result;
        }
    }

}