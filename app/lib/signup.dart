import "package:flutter/material.dart";
import "package:http/http.dart" as http;
import "dart:convert";
import 'package:shared_preferences/shared_preferences.dart';

class SignUpPage extends StatefulWidget {
	@override
	SignUpPageState createState() {
		return SignUpPageState();
	}
}

class SignUpPageState extends State<SignUpPage> {
	var _currentIndex = 0;
	var _title = "Signup";
	List<Widget> _navigation;
	final _signUpFormKey = GlobalKey<FormState>();
	final _loginFormKey = GlobalKey<FormState>();
	final _signUpPinKey = GlobalKey<FormFieldState>();
	final _loginPinKey = GlobalKey<FormFieldState>();

	String _email;
	String _pin;

	Widget _pinTextFormField(String hintText, String labelText, Function(String) validator, {GlobalKey<FormFieldState> key}) {
		return TextFormField(
			key: key,
			keyboardType: TextInputType.number,
			maxLength: 4,
			obscureText: true,
			decoration: InputDecoration(hintText: hintText, labelText: labelText, counterText: ""),
			validator: validator,
			onSaved: (value) {
				setState(() {
					_pin = value;
				});
			});
	}

	@override
	void initState() {
		super.initState();
		_navigation = List<Widget>();
		_navigation.add(Form(
			key: _signUpFormKey,
			child: Padding(
				padding: EdgeInsets.all(26.0),
				child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: <Widget>[
					TextFormField(
						decoration: InputDecoration(hintText: "Enter your email address", labelText: "Email"),
						validator: _validation,
						onSaved: (value) {
							setState(() {
								_email = value;
							});
						},
					),
					_pinTextFormField("Enter a 4 digit pin", "Pin", _pinValidation, key: _signUpPinKey),
					_pinTextFormField("Re-enter your 4 digit pin", "Confirm Pin", _confirmPinValidation),
					RaisedButton(
						onPressed: () async {
							if (_signUpFormKey.currentState.validate()) {
								_signUpFormKey.currentState.save();
								var url = "https://www.cs.kent.ac.uk/projects/IsThisAMood/api/signup";
								var body = json.encode({"email": _email, "pin": _pin});

								Map<String, String> headers = {
									'Content-type': 'application/json',
									'Accept': 'application/json',
								};
								var response = await http.post(url, body: body, headers: headers);

								if (response.statusCode != 200)
									Scaffold.of(context).showSnackBar(SnackBar(content: Text("There was a network error, try again.")));
								else {
									var responseJson = json.decode(response.body);
									final prefs = await SharedPreferences.getInstance();
									print(responseJson["access_token"]);
									prefs.setString('access_token', responseJson["access_token"]);
									prefs.setString('pin', _signUpPinKey.currentState.value);
									Navigator.of(context).pushNamedAndRemoveUntil('/home', (Route<dynamic> route) => false);
								}
							}
						},
						child: Text("Signup"))
				]))));
		_navigation.add(Form(
			key: _loginFormKey,
			child: Padding(
				padding: EdgeInsets.all(26.0),
				child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: <Widget>[
					TextFormField(
						decoration: InputDecoration(hintText: "Enter your email address", labelText: "Email"),
						validator: _validation,
						onSaved: (value) {
							setState(() {
								_email = value;
							});
						},
					),

					_pinTextFormField("Enter your pin", "Pin", _pinValidation),

					RaisedButton(
						onPressed: () async {
//                    if (_loginFormKey.currentState.validate()) {
//                      _loginFormKey.currentState.save();
//                      var url = "https://www.cs.kent.ac.uk/projects/IsThisAMood/api/signup";
//                      var body = json.encode({"email": _email, "pin": _pin});
//
//                      Map<String, String> headers = {
//                        'Content-type': 'application/json',
//                        'Accept': 'application/json',
//                      };
//                      var response = await http.post(url, body: body, headers: headers);
//
//                      if (response.statusCode != 200)
//                        Scaffold.of(context).showSnackBar(SnackBar(content: Text("There was a network error, try again.")));
//                      else {
//                        var responseJson = json.decode(response.body);
//                        final prefs = await SharedPreferences.getInstance();
//                        print(responseJson["access_token"]);
//                        prefs.setString('access_token', responseJson["access_token"]);
//                        prefs.setString('pin', _loginPinKey.currentState.value);
//                        Navigator.of(context).pushNamedAndRemoveUntil('/home', (Route<dynamic> route) => false);
//                      }
//                    }
						},
						child: Text("Login"))
				]))));
	}

	@override
	Widget build(BuildContext context) {
		return Scaffold(
			appBar: AppBar(title: Text(_title)),
			body: IndexedStack(
				index: _currentIndex,
				children: _navigation,
			),
			bottomNavigationBar: BottomNavigationBar(items: const <BottomNavigationBarItem>[
				BottomNavigationBarItem(
					icon: Icon(Icons.add_box),
					title: Text('Sign Up'),
				),
				BottomNavigationBarItem(
					icon: Icon(Icons.account_box),
					title: Text('Login'),
				),

			], currentIndex: _currentIndex,
				selectedItemColor: Colors.amber[800],
				onTap: _onItemTapped));
	}

	void _onItemTapped(int index) {
		if (_currentIndex != index)
			setState(() {
				_title = "Sign Up";
				if(index == 1)
					_title = "Login";

				_currentIndex = index;
			});
	}

	String _validation(String value) {
		if (value.isEmpty) return "Please enter something";
		return null;
	}

	String _pinValidation(String value) {
		var result = _validation(value);
		if (result != null) return result;
		if (value.length != 4) return "Pin must be 4 digits";
		return null;
	}

	String _confirmPinValidation(String value) {
		var result = _validation(value);
		if (result != null) return result;
		if (_signUpPinKey.currentState.value != value) return "Pins need to be the same";
		return null;
	}
}
