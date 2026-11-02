using CinemaSystemRandomPlay.Domain.Catalogo;
using CinemaSystemRandomPlay.Infrastructure.Persistence.Funciones;

namespace CinemaSystemRandomPlay.Infrastructure.Persistence;

/// <summary>
/// Películas de Studio Ghibli para desarrollo, copiadas del mock del frontend
/// (cinema-system-randomplay-frontend, scripts/mock-data/catalogo.json). Título, duración, sinopsis
/// (en inglés) y póster vienen de https://ghibliapi.vercel.app/films; la clasificación y los
/// horarios los inventa el generador del mock de forma determinística, así que no son reales.
/// Cada función es (sala, minutos desde el arranque, precio en ARS); las que no tienen funciones
/// muestran la marca "sin funciones disponibles".
/// </summary>
public static partial class DevDataSeeder
{
    private sealed record PeliculaConFunciones(Pelicula Pelicula, (Guid SalaId, int MinutosDesdeAhora, decimal Precio)[] Funciones);

    private static PeliculaConFunciones[] PeliculasGhibli() =>
    [
        new(
            new Pelicula(
                new Guid("2baf70d1-42bb-4437-b551-e5fed5a87abe"),
                "Castle in the Sky",
                124,
                Clasificacion.ATP,
                "The orphan Sheeta inherited a mysterious crystal that links her to the mythical sky-kingdom of Laputa. With the help of resourceful Pazu and a rollicking band of sky pirates, she makes her way to the ruins of the once-great civilization. Sheeta and Pazu must outwit the evil Muska, who plans to use Laputa's science to make himself ruler of the world.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/npOnzAbLh6VOIu3naU5QaEcTepo.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro1Id, 45, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 74, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 195, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 224, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 374, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("12cfb892-aac0-4c5b-94af-521852e46d6a"),
                "Grave of the Fireflies",
                89,
                Clasificacion.Mas13,
                "In the latter part of World War II, a boy and his sister, orphaned when their mother is killed in the firebombing of Tokyo, are left to survive on their own in what remains of civilian life in Japan. The plot follows this boy and his sister as they do their best to survive in the Japanese countryside, battling hunger, prejudice, and pride in their own quiet, personal battle.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/qG3RYlIVpTYclR9TYIsy8p7m7AT.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro2Id, 62, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 91, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 212, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 241, 4500.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 362, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 391, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 541, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("58611129-2dbc-4a81-a72f-77ddfc1b1b49"),
                "My Neighbor Totoro",
                86,
                Clasificacion.ATP,
                "Two sisters move to the country with their father in order to be closer to their hospitalized mother, and discover the surrounding trees are inhabited by Totoros, magical spirits of the forest. When the youngest runs away from home, the older sister seeks help from the spirits to find her.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/rtGDOeG9LzoerkDGZF9dnVeLppL.jpg"),
            [
                (SalaEntityConfiguration.SalaSur1Id, 47, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 79, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 108, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 197, 5200.50m),
                (SalaEntityConfiguration.SalaCentro2Id, 229, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 258, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 347, 4500.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 379, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 529, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("ea660b10-85c4-4ae3-8a5f-41cea3648e3e"),
                "Kiki's Delivery Service",
                102,
                Clasificacion.Mas16,
                "A young witch, on her mandatory year of independent life, finds fitting into a new community difficult while she supports herself by running an air courier service.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/7nO5DUMnGUuXrA4r2h6ESOKQRrx.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro2Id, 96, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 125, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 246, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 275, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 425, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("4e236f34-b981-41c3-8c65-f8c9000b94e7"),
                "Only Yesterday",
                118,
                Clasificacion.Mas13,
                "It’s 1982, and Taeko is 27 years old, unmarried, and has lived her whole life in Tokyo. She decides to visit her family in the countryside, and as the train travels through the night, memories flood back of her younger years: the first immature stirrings of romance, the onset of puberty, and the frustrations of math and boys. At the station she is met by young farmer Toshio, and the encounters with him begin to reconnect her to forgotten longings. In lyrical switches between the present and the past, Taeko contemplates the arc of her life, and wonders if she has been true to the dreams of her childhood self.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/xjJU6rwzLX7Jk8HFQfVW6H5guMC.jpg"),
            [
                (SalaEntityConfiguration.SalaSur1Id, 52, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 113, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 202, 4500.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 263, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 352, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 413, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 502, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("ebbb6b7c-945c-41ee-a792-de0e43191bd8"),
                "Porco Rosso",
                93,
                Clasificacion.ATP,
                "Porco Rosso, known in Japan as Crimson Pig (Kurenai no Buta) is the sixth animated film by Hayao Miyazaki and released in 1992. You're introduced to an Italian World War I fighter ace, now living as a freelance bounty hunter chasing 'air pirates' in the Adriatic Sea. He has been given a curse that changed his head to that of a pig. Once called Marco Pagot, he is now known to the world as 'Porco Rosso', Italian for 'Red Pig.'",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/byKAndF6KQSDpGxp1mTr23jPbYp.jpg"),
            []),
        new(
            new Pelicula(
                new Guid("1b67aa9a-2e4a-45af-ac98-64d6ad15b16c"),
                "Pom Poko",
                119,
                Clasificacion.Mas18,
                "As the human city development encroaches on the raccoon population's forest and meadow habitat, the raccoons find themselves faced with the very real possibility of extinction. In response, the raccoons engage in a desperate struggle to stop the construction and preserve their home.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/kowo9E1e1JcWLXj9cCvAOFZcy5n.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro1Id, 57, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 86, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 207, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 236, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 386, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("ff24da26-a969-4f0e-ba1e-a122ead6c6e3"),
                "Whisper of the Heart",
                111,
                Clasificacion.ATP,
                "Shizuku lives a simple life, dominated by her love for stories and writing. One day she notices that all the library books she has have been previously checked out by the same person: 'Seiji Amasawa'. Curious as to who he is, Shizuku meets a boy her age whom she finds infuriating, but discovers to her shock that he is her 'Prince of Books'. As she grows closer to him, she realises that he merely read all those books to bring himself closer to her. The boy Seiji aspires to be a violin maker in Italy, and it is his dreams that make Shizuku realise that she has no clear path for her life. Knowing that her strength lies in writing, she tests her talents by writing a story about Baron, a cat statuette belonging to Seiji's grandfather.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/5E3Hvbu0bg38ouYf6chGftVGqZ7.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro2Id, 74, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 103, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 224, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 253, 4500.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 374, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 403, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 553, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("0440483e-ca0e-4120-8c50-4c8cd9b965d6"),
                "Princess Mononoke",
                134,
                Clasificacion.Mas13,
                "Ashitaka, a prince of the disappearing Ainu tribe, is cursed by a demonized boar god and must journey to the west to find a cure. Along the way, he encounters San, a young human woman fighting to protect the forest, and Lady Eboshi, who is trying to destroy it. Ashitaka must find a way to bring balance to this conflict.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/jHWmNr7m544fJ8eItsfNk8fs2Ed.jpg"),
            [
                (SalaEntityConfiguration.SalaSur1Id, 59, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 91, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 120, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 209, 5200.50m),
                (SalaEntityConfiguration.SalaCentro2Id, 241, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 270, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 359, 4500.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 391, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 541, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("45204234-adfd-45cb-a505-a8e7a676b114"),
                "My Neighbors the Yamadas",
                104,
                Clasificacion.ATP,
                "The Yamadas are a typical middle class Japanese family in urban Tokyo and this film shows us a variety of episodes of their lives. With tales that range from the humourous to the heartbreaking, we see this family cope with life's little conflicts, problems and joys in their own way.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/wTGuHmMIBBgKakY80J1D52VvQKI.jpg"),
            [
                (SalaEntityConfiguration.SalaNorte1Id, 47, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 108, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 197, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 258, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 347, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("dc2e6bd1-8156-4886-adff-b39e6043af0c"),
                "Spirited Away",
                124,
                Clasificacion.Mas16,
                "Spirited Away is an Oscar winning Japanese animated film about a ten year old girl who wanders away from her parents along a path that leads to a world ruled by strange and unusual monster-like animals. Her parents have been changed into pigs along with others inside a bathhouse full of these creatures. Will she ever see the world how it once was?",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/39wmItIWsg5sZMyRUHLkWBcuVCM.jpg"),
            [
                (SalaEntityConfiguration.SalaSur1Id, 64, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 125, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 214, 4500.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 275, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 364, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 425, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 514, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("90b72513-afd4-4570-84de-a56c312fdf81"),
                "The Cat Returns",
                75,
                Clasificacion.Mas13,
                "Haru, a schoolgirl bored by her ordinary routine, saves the life of an unusual cat and suddenly her world is transformed beyond anything she ever imagined. The Cat King rewards her good deed with a flurry of presents, including a very shocking proposal of marriage to his son! Haru embarks on an unexpected journey to the Kingdom of Cats where her eyes are opened to a whole other world.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/avPMO5cnaGHgLaNiAIhy33WoQLm.jpg"),
            []),
        new(
            new Pelicula(
                new Guid("cd3d059c-09f4-4ff3-8d63-bc765a5184fa"),
                "Howl's Moving Castle",
                119,
                Clasificacion.ATP,
                "When Sophie, a shy young woman, is cursed with an old body by a spiteful witch, her only chance of breaking the spell lies with a self-indulgent yet insecure young wizard and his companions in his legged, walking home.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/TkTPELv4kC3u1lkloush8skOjE.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro1Id, 69, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 98, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 219, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 248, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 398, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("112c1e67-726f-40b1-ac17-6974127bb9b9"),
                "Tales from Earthsea",
                116,
                Clasificacion.Mas18,
                "Something bizarre has come over the land. The kingdom is deteriorating. People are beginning to act strange... What's even more strange is that people are beginning to see dragons, which shouldn't enter the world of humans. Due to all these bizarre events, Ged, a wandering wizard, is investigating the cause. During his journey, he meets Prince Arren, a young distraught teenage boy. While Arren may look like a shy young teen, he has a severe dark side, which grants him strength, hatred, ruthlessness and has no mercy, especially when it comes to protecting Teru. For the witch Kumo this is a perfect opportunity. She can use the boy's 'fears' against the very one who would help him, Ged.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/67yYwCPq7NbxSF6BIIXCMD34sY0.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro2Id, 86, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 115, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 236, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 265, 4500.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 386, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 415, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 565, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("758bf02e-3122-46e0-884e-67cf83df1786"),
                "Ponyo",
                100,
                Clasificacion.ATP,
                "The son of a sailor, 5-year old Sosuke lives a quiet life on an oceanside cliff with his mother Lisa. One fateful day, he finds a beautiful goldfish trapped in a bottle on the beach and upon rescuing her, names her Ponyo. But she is no ordinary goldfish. The daughter of a masterful wizard and a sea goddess, Ponyo uses her father's magic to transform herself into a young girl and quickly falls in love with Sosuke, but the use of such powerful sorcery causes a dangerous imbalance in the world. As the moon steadily draws nearer to the earth and Ponyo's father sends the ocean's mighty waves to find his daughter, the two children embark on an adventure of a lifetime to save the world and fulfill Ponyo's dreams of becoming human.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/mikKSEdk5kLhflWXbp4S5mmHsDo.jpg"),
            [
                (SalaEntityConfiguration.SalaSur1Id, 71, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 103, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 132, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 221, 5200.50m),
                (SalaEntityConfiguration.SalaCentro2Id, 253, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 282, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 371, 4500.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 403, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 553, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("2de9426b-914a-4a06-a3a0-5e6d9d3886f6"),
                "Arrietty",
                94,
                Clasificacion.Mas13,
                "14-year-old Arrietty and the rest of the Clock family live in peaceful anonymity as they make their own home from items 'borrowed' from the house's human inhabitants. However, life changes for the Clocks when a human boy discovers Arrietty.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/oc2OB2KDmSRDMelKEAA1n4YRQL0.jpg"),
            [
                (SalaEntityConfiguration.SalaNorte1Id, 59, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 120, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 209, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 270, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 359, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("45db04e4-304a-4933-9823-33f389e8d74d"),
                "From Up on Poppy Hill",
                91,
                Clasificacion.ATP,
                "The story is set in 1963 in Yokohama. Kokuriko Manor sits on a hill overlooking the harbour. A 16 year-old girl, Umi, lives in that house. Every morning she raises a signal flag facing the sea. The flag means “I pray for safe voyages”. A 17 year-old boy, Shun, always sees this flag from the sea as he rides a tugboat to school. Gradually the pair are drawn to each other but they are faced with a sudden trial. Even so, they keep going without running from facing the hardships of reality.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/rRLYX4RZIyloHSJwvZKAhphAjiB.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro1Id, 47, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 76, 5200.50m),
                (SalaEntityConfiguration.SalaCentro2Id, 197, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 226, 4500.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 347, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 376, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 526, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("67405111-37a5-438f-81cc-4666af60c800"),
                "The Wind Rises",
                126,
                Clasificacion.Mas16,
                "A lifelong love of flight inspires Japanese aviation engineer Jiro Horikoshi, whose storied career includes the creation of the A-6M World War II fighter plane.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/jfwSexzlIzaOgxP9A8bTA6t8YYb.jpg"),
            []),
        new(
            new Pelicula(
                new Guid("578ae244-7750-4d9f-867b-f3cd3d6fecf4"),
                "The Tale of the Princess Kaguya",
                137,
                Clasificacion.Mas13,
                "A bamboo cutter named Sanuki no Miyatsuko discovers a miniature girl inside a glowing bamboo shoot. Believing her to be a divine presence, he and his wife decide to raise her as their own, calling her 'Princess'.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/mWRQNlWXYYfd2z4FRm99MsgHgiA.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro1Id, 81, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 110, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 231, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 260, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 410, 4500.00m),
            ]),
        new(
            new Pelicula(
                new Guid("5fdfb320-2a02-49a7-94ff-5ca418cae602"),
                "When Marnie Was There",
                103,
                Clasificacion.ATP,
                "The film follows Anna Sasaki living with her relatives in the seaside town. Anna comes across a nearby abandoned mansion, where she meets Marnie, a mysterious girl who asks her to promise to keep their secrets from everyone. As the summer progresses, Anna spends more time with Marnie, and eventually Anna learns the truth about her family and foster care.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/vug1dvDI1tSa60Z8qjCuUE7ntkO.jpg"),
            [
                (SalaEntityConfiguration.SalaCentro2Id, 98, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 127, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 248, 5200.50m),
                (SalaEntityConfiguration.SalaSur1Id, 277, 4500.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 398, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 427, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 577, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("d868e6ec-c44a-405b-8fa6-f7f0f8cfb500"),
                "The Red Turtle",
                80,
                Clasificacion.Mas18,
                "A man set adrift by a storm wakes up on a beach. He discovers that he is on a deserted island with plenty of fresh water, fruit and a dense bamboo forest. He builds a raft from bamboo and attempts to sail away, but his raft is destroyed by an unseen monster in the sea, forcing him back to the island. He tries again with another, larger raft, but is again foiled by the creature. A third attempt again ends with the raft destroyed, but this time he is confronted by a giant red turtle, which stares at him, and forces him back to the island.",
                imagenUrl: "https://image.tmdb.org/t/p/w600_and_h900_bestv2/wOBU3SLjQ9358Km9YWYasPZyebp.jpg"),
            [
                (SalaEntityConfiguration.SalaNorte1Id, 54, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 83, 4800.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 115, 5200.50m),
                (SalaEntityConfiguration.SalaNorte1Id, 204, 4800.00m),
                (SalaEntityConfiguration.SalaSur1Id, 233, 5200.50m),
                (SalaEntityConfiguration.SalaCentro2Id, 265, 4500.00m),
                (SalaEntityConfiguration.SalaSur1Id, 383, 4500.00m),
                (SalaEntityConfiguration.SalaCentro1Id, 415, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 565, 5200.50m),
            ]),
        new(
            new Pelicula(
                new Guid("790e0028-a31c-4626-a694-86b7a8cada40"),
                "Earwig and the Witch",
                82,
                Clasificacion.ATP,
                "An orphan girl, Earwig, is adopted by a witch and comes home to a spooky house filled with mystery and magic.",
                imagenUrl: "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/sJhFtY3eHuvvACaPpxpzdCLQqpQ.jpg"),
            [
                (SalaEntityConfiguration.SalaNorte1Id, 71, 4800.00m),
                (SalaEntityConfiguration.SalaCentro2Id, 132, 4500.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 221, 5200.50m),
                (SalaEntityConfiguration.SalaCentro1Id, 282, 4800.00m),
                (SalaEntityConfiguration.SalaNorte1Id, 371, 4500.00m),
            ]),
    ];
}
