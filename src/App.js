import React, {Fragment, useState, useEffect} from "react";
import "./App.css";

const AuthDisplay = () => (
  <section>
    <h1> Welcome to the registry lookup guide</h1>
    <h3>Press the button below to authenticate and fetch information about the person.</h3>
    <div className="auth">
      <img src="./images/auth.png" alt="auth" />
      <form action="/authentication-session" method="POST">
        <button type="submit">
          Start lookup
        </button>
      </form>
    </div>
  </section>
);

const Message = ({message}) => (
  <section>
    {message.name ?
      <Fragment>
        <h2>Successfull lookup for NIN: {message.nin}</h2>
        <p>First Name: {message.name}</p>
        <p>Last Name: {message.lastName}</p>
        <p>Middle Name: {message.middleName}</p>
        <p>Birth date: {message.birthDate} </p>
        <p>Birth location: {message.birth} </p> 
        <p>Birth country: {message.birthCountry} </p> 
        <p>Citizenship: {message.citizenship} </p> 
        <p>Address: {message.personStreet}, {message.personPostalCode} {message.personCity}, {message.personCountry} </p> 
        <p>Source of information: {message.lookupSource} </p> 
      </Fragment>
      : <p>The sign in process was aborted or has an error."</p>}
  </section>
);

function App() {
  const [message, setMessage] = useState(null);

  useEffect(() => {
    const query = new URLSearchParams(window.location.search);

    if (query.get("success")) {
      setMessage({name: query.get("name"), lastName: query.get("lastName"), middleName: query.get("middleName"), nin: query.get("nin"), birth: query.get("birth"), birthDate: query.get("birthDate"), 
      birthCountry: query.get("birthCountry"), citizenship: query.get("citizenship"), personStreet: query.get("personStreet"),
      personPostalCode: query.get("personPostalCode"), personCity: query.get("personCity"), personCountry: query.get("personCountry"),
      lookupSource: query.get("lookupSource")});
    }
    if (query.get("canceled")) {
      setMessage({});
    }
    if (query.get("error")) {
      setMessage({});
    }
  }, []);

  return message ? (
    <Message message={message} />
  ) : (
    <AuthDisplay />
  );
}

export default App;
