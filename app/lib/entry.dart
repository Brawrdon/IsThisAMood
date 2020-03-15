class Entry {
  String id;
  String name;
  String mood;
  String rating;
  List<dynamic> activities;

  Entry(this.name, this.mood, this.rating, this.activities);

  Entry.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        name = json['name'],
        mood = json['mood'],
        rating = json['rating'],
        activities = json['activities'];
}
