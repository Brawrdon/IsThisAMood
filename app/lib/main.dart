import 'dart:async';
import 'dart:io';

import 'package:isthisamood/create.dart';
import 'package:isthisamood/signup.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import "package:http/http.dart" as http;
import "dart:convert";
import 'package:isthisamood/entry.dart';
import 'package:flutter/foundation.dart';
import 'package:csv/csv.dart';
import 'package:flutter/services.dart' show rootBundle;

final appTitle = "Is This A Mood";

void main() => runApp(StartScreen());

class StartScreen extends StatelessWidget {
	@override
	Widget build(BuildContext context) {
		Future<SharedPreferences> getSharedPreferences = SharedPreferences.getInstance();

		Widget _getHomeScreen() {
			return new FutureBuilder(
				future: getSharedPreferences,
				builder: (context, snapshot) {
					String accessToken;
					String pin;
					// if (!kReleaseMode) return Home(accessToken: "063f4190-f45f-41bd-a030-9469e472c54b");
					if (snapshot.hasData) {
						SharedPreferences sharedPreferences = snapshot.data;
						accessToken = sharedPreferences.getString("access_token") ?? null;
						pin = sharedPreferences.getString("pin") ?? null;

						if (accessToken != null) {
							return Home(accessToken: accessToken, pin: pin);
						} else
							return SignUpPage();
					} else if (snapshot.hasError) return Text(snapshot.error);

					return Text("");
				});
		}

		return MaterialApp(
			title: appTitle,
			theme: ThemeData(
				primarySwatch: Colors.blue,
			),
			home: _getHomeScreen(),
			routes: {"/home": (context) => StartScreen()},
		);
	}
}

class Home extends StatefulWidget {
	final String accessToken;
	final String pin;

	Home({Key key, this.accessToken, this.pin}) : super(key: key);

	@override
	State<StatefulWidget> createState() {
		return _HomeState();
	}
}

class _HomeState extends State<Home> {
	var _currentIndex = 0;
	List<Widget> _navigation;
	StreamController<List<Entry>> _entriesStream;

	@override
	void initState() {
		super.initState();
		_entriesStream = StreamController<List<Entry>>();
		_navigation = _getNavigationItems();
	}

	@override
	Widget build(BuildContext context) {
		return Scaffold(
			body: IndexedStack(
				index: _currentIndex,
				children: _navigation,
			),
			appBar: AppBar(title: Text(appTitle), actions: <Widget>[
				// action button
				IconButton(
					icon: Icon(Icons.refresh),
					onPressed: () async {
						await _getEntries();
					})
			]),
			floatingActionButton: FloatingActionButton(
				onPressed: () {
					Navigator.push(context, MaterialPageRoute(builder: (context) =>
						CreateEntry(update: () async => await _getEntries())),
					);
				},
				child: Icon(Icons.add),
			),
			bottomNavigationBar: BottomNavigationBar(items: const <BottomNavigationBarItem>[
				BottomNavigationBarItem(
					icon: Icon(Icons.book),
					title: Text('Entries'),
				),
				BottomNavigationBarItem(
					icon: Icon(Icons.table_chart),
					title: Text('Summary'),
				),
				BottomNavigationBarItem(
					icon: Icon(Icons.help),
					title: Text('Help'),
				),
			], currentIndex: _currentIndex,
				selectedItemColor: Colors.amber[800],
				onTap: _onItemTapped));
	}

	List<Widget> _getNavigationItems() {
		final list = List<Widget>();
		list.add(_buildEntryView());
		list.add(Text("Summary"));
		list.add(Text("Help"));

		return list;
	}

	void _onItemTapped(int index) {
		if (_currentIndex != index)
			setState(() {
				_currentIndex = index;
			});
	}

	Future<void> _getEntries() async {

		var url = "https://www.cs.kent.ac.uk/projects/IsThisAMood/api/app/entries";
		Map<String, String> headers = {"Authorization": widget.accessToken, "Pin": widget.pin};
		var response = await http.get(url, headers: headers);
		if (response.statusCode == 200) {
			List jsonResponse = json.decode(response.body);
			_entriesStream.add(jsonResponse.map((entry) => new Entry.fromJson(entry)).toList());
		} else {
			throw Exception('Failed to get entries');
		}
	}

	Widget _buildEntryView() {
		return FutureBuilder(
			future: _getEntries(),
			builder: (context, snapshot) {
				if (snapshot.connectionState == ConnectionState.done) {
					return StreamBuilder(
						stream: _entriesStream.stream,
						builder: (context, snapshot) {
							if (snapshot.hasError) return Text('Error: ${snapshot.error}');
							if (snapshot.connectionState == ConnectionState.active) {
								if (snapshot.data == null || snapshot.data.length == 0)
									return Text("You have no entries");

								return ListView.builder(
									itemBuilder: (context, index) {
										var entry = snapshot.data[index];
										return ListTile(
											title: Text("${entry.name}"),
											subtitle: Text("Mood: ${entry.mood}")
										);
									},
									itemCount: snapshot.data.length);
							}
							else {
								return Container();
							}
						});
				} else {
					return Container();
				}
			});
	}
}

