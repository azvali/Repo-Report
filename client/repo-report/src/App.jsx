import './App.css'
import { useState } from 'react'

function App() {
  const [overall, setOverall] = useState('');
  const [single, setSingle] = useState([]);

  
  const [num, setNum] = useState('');
  const [url, setUrl] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();


    //filtering - validate
    if(!num || !url){
      alert("Please fill all inputs.");
    }
    if(num > 30){
      alert("30 commits max.");
    }

    const response = await fetch("http://localhost:5135/api/getSummaries", {
      method: "POST",
      headers : {"Content-Type" : "application/json"},
      body : JSON.stringify({
        num : num,
        url : url
      })
    })
    
    const data = await response.json();

    //do something
    console.log(data);

  };


  return (
    <>
      <div className='header'>
        <h1>Repo Report</h1>
      </div>
      <form className='input-field'>
        <div>
          <input type='number' placeholder='#' value={num} onChange={(e) => {setNum(e.target.value)}}></input>
          <input type='text' placeholder='Github URL' value={url} onChange={(e) => {setUrl(e.target.value)}}></input>
        </div>
        <button type='submit' onClick={(e) => {handleSubmit(e)}}>Summerize</button>
      </form>
      <div className='output'>
        <div className='overall-summary'>
          {overall}
        </div>
        <div className='single-commit'>
          {/* single commit summary cards */}
        </div>
      </div>
    </>
  )
}

export default App
