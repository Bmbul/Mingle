using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mingle.DAL.Content;
using Mingle.DAL.Entities;

#nullable disable

namespace Mingle.DAL.Migrations
{
    /// <summary>The topics cards are about.</summary>
    [DbContext(typeof(MingleDbContext))]
    [Migration("20261128100000_AddStarterTopics")]
    public sealed class AddStarterTopics : ContentMigration
    {
        public override ContentChange Content() => new()
        {
            Topics =
            [
                Topic("everyday", "Everyday life", "Առօրյա", "Повседневность", about: "ordinary days, routines, small pleasures and annoyances"),
                Topic("childhood", "Childhood", "Մանկություն", "Детство", about: "growing up: memories, games, school, how they were as kids"),
                Topic("food", "Food", "Ուտելիք", "Еда", about: "eating, cooking, favourite dishes, food habits"),
                Topic("habits", "Habits", "Սովորություններ", "Привычки", about: "daily habits, quirks, routines, small rituals"),
                Topic("what-if", "What if…", "Իսկ եթե…", "А что если…", about: "imaginary situations and hypotheticals"),
                Topic("home", "Home", "Տուն", "Дом", about: "the place you live, what makes a place feel like home"),
                Topic("travel", "Travel", "Ճամփորդություն", "Путешествия", about: "trips, places, journeys and travel plans"),
                Topic("funny", "Funny moments", "Զվարճալի պահեր", "Смешные моменты", about: "funny stories, embarrassing moments, things that make you laugh"),
                Topic("friendship", "Friendship", "Ընկերություն", "Дружба", TopicAudience.Friends, about: "friends, what makes a good friend, shared memories"),
                Topic("dreams", "Dreams", "Երազանքներ", "Мечты", about: "hopes, ambitions and wishes for your life (not dreams while sleeping)"),
                Topic("values", "Values", "Արժեքներ", "Ценности", about: "what matters to you, principles, beliefs, right and wrong"),
                Topic("memories", "Memories", "Հիշողություններ", "Воспоминания", about: "meaningful moments from the past"),
                Topic("music", "Music", "Երաժշտություն", "Музыка", about: "songs, artists, concerts and music in your life"),
                Topic("fears", "Fears", "Վախեր", "Страхи", about: "worries, fears and what helps you face them"),
                Topic("future", "Future plans", "Ապագայի ծրագրեր", "Планы на будущее", about: "plans and expectations for the coming years"),
                Topic("us", "Us", "Մենք", "Мы", TopicAudience.Couples, pickable: false, about: "the two players as a couple"),
                Topic("love", "Love", "Սեր", "Любовь", TopicAudience.Couples, about: "love, affection and how you show and feel it"),
                Topic("long-distance", "Long distance", "Հեռավոր հարաբերություններ", "Отношения на расстоянии", TopicAudience.Couples, about: "living apart: missing each other, staying close, reunions"),
                Topic("dates", "Dates", "Ժամադրություններ", "Свидания", TopicAudience.Couples, about: "dates, romantic plans and time together"),
                Topic("needs", "Needs", "Կարիքներ", "Потребности", TopicAudience.Couples, about: "what you need from a partner to feel loved and safe"),
                Topic("conflict", "Arguments", "Վեճեր", "Ссоры", TopicAudience.Couples, about: "disagreements and how to handle and repair them"),
                Topic("family", "Family", "Ընտանիք", "Семья", about: "parents, siblings, relatives and family life"),
                Topic("work", "Work and ambition", "Աշխատանք և նպատակներ", "Работа и амбиции", about: "jobs, careers, ambition and what you want from work"),
                Topic("money", "Money", "Փող", "Деньги", about: "spending, saving and how you feel about money"),
                Topic("hobbies", "Hobbies", "Հոբբիներ", "Хобби", about: "free-time activities, interests and things you love doing"),
                Topic("regrets", "Regrets", "Ափսոսանքներ", "Сожаления", about: "choices you would make differently and what you learned"),
                Topic("firsts", "Firsts", "Առաջին անգամներ", "Первые разы", about: "first times: first trips, jobs, loves, experiences"),
                Topic("gratitude", "Gratitude", "Երախտագիտություն", "Благодарность", about: "what and who you are thankful for"),
                Topic("adventure", "Adventure", "Արկածներ", "Приключения", about: "risk, new experiences and stepping out of your comfort zone"),
                Topic("stories", "Movies and books", "Ֆիլմեր և գրքեր", "Фильмы и книги", about: "films, series and books you love or that changed you"),
                Topic("growth", "Personal growth", "Անձնական աճ", "Личностный рост", about: "how you have changed, learning and becoming a better person"),
            ],
        };
    }
}
