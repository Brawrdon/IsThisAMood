import 'package:isthisamood/signup.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() => runApp(Home());

class Home extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final appTitle = "Is This A Mood";

    Future<SharedPreferences> getSharedPreferences = SharedPreferences.getInstance();

    Widget _getHomeScreen() {
      return new FutureBuilder(
          future: getSharedPreferences,
          builder: (context, snapshot) {
            String accessCode;

            if (snapshot.hasData) {
              SharedPreferences sharedPreferences = snapshot.data;
              accessCode = sharedPreferences.getString("accessCode") ?? null;
            } else if (snapshot.hasError)
              return Text(snapshot.error);
            else
              return Text("Loading");

            if (accessCode != null)
              return Text("Hey");
            else
              return SignUpForm();
          });
    }

    return MaterialApp(
      title: appTitle,
      theme: ThemeData(
        primarySwatch: Colors.blue,
      ),
      home: Scaffold(
        appBar: AppBar(
          title: Text(
            appTitle,
          ),
        ),
        body: _getHomeScreen(),
      ),
    );
  }
}
