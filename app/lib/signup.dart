import "package:flutter/material.dart";

class SignUpForm extends StatefulWidget {
  @override
  SignUpFormState createState() {
    return SignUpFormState();
  }
}

class SignUpFormState extends State<SignUpForm> {
  final _formKey = GlobalKey<FormState>();
  final _pinKey = GlobalKey<FormFieldState>();

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
  Widget build(BuildContext context) {
    return Form(
        key: _formKey,
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
              _pinTextFormField("Enter a 4 digit pin", "Pin", _pinValidation, key: _pinKey),
              _pinTextFormField("Re-enter your 4 digit pin", "Confirm Pin", _confirmPinValidation),
              RaisedButton(
                  onPressed: () {
                    if (_formKey.currentState.validate()) {
                      _formKey.currentState.save();
                    }
                  },
                  child: Text("Submit"))
            ])));
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
    if (_pinKey.currentState.value != value) return "Pins need to be the same";
    return null;
  }
}
