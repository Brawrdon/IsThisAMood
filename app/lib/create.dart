import 'dart:io';

import "package:flutter/material.dart";
import "package:http/http.dart" as http;
import 'package:isthisamood/entry.dart';
import "dart:convert";
import 'package:shared_preferences/shared_preferences.dart';

class CreateEntry extends StatefulWidget {
	final Function update;

	const CreateEntry({Key key, this.update}) : super(key: key);

	@override
	CreateEntryState createState() {
		return CreateEntryState();
	}
}

class CreateEntryState extends State<CreateEntry> {
	final _formKey = GlobalKey<FormState>();
	int _rating = 0;

	@override
	void initState() {
		super.initState();
	}

	@override
	Widget build(BuildContext context) {
		return Scaffold(
			appBar: AppBar(title: Text("Add an entry")),
			body: Form(
				key: _formKey,
				child: Padding(
					padding: EdgeInsets.all(26.0),
					child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: <Widget>[
						TextFormField(
							decoration: InputDecoration(hintText: "Enter a title", labelText: "Title"),
							validator: _validation
						),
						DropdownButtonFormField(
							decoration: InputDecoration(hintText: "How strongly would you rate this feeling?", labelText: "Rating"),
							items: <DropdownMenuItem>[
								DropdownMenuItem(child: Text("0"), value: 0,),
								DropdownMenuItem(child: Text("1"), value: 1,),
								DropdownMenuItem(child: Text("2"), value: 2,),
								DropdownMenuItem(child: Text("3"), value: 3,),
								DropdownMenuItem(child: Text("4"), value: 4,),
								DropdownMenuItem(child: Text("5"), value: 5,),
								DropdownMenuItem(child: Text("6"), value: 6,),
								DropdownMenuItem(child: Text("7"), value: 7,),
								DropdownMenuItem(child: Text("8"), value: 8,),
								DropdownMenuItem(child: Text("9"), value: 9,),
								DropdownMenuItem(child: Text("10"), value: 10,),
							],
							value: _rating,
							onChanged: (value) {
							    setState(() {
                                    _rating = value;
                                });
							},
						),
						RaisedButton(
							onPressed: () {
							},
							child: Text("Submit"))
					]))));
	}

	String _validation(String value) {
		if (value.isEmpty) return "Please enter something";
		return null;
	}


}
