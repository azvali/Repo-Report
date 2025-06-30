import './App.css'
import { useState } from 'react'

function App() {
  const [overall, setOverall] = useState('');
  const [single, setSingle] = useState([]);
  const [num, setNum] = useState('');
  const [url, setUrl] = useState('');

  const handleSubmit = (e) => {
    e.preventDefault();


    //filtering - validate


    //do something


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
          {/* overall summary card */}
        </div>
        <div className='single-commit'>
          {/* single commit summary cards */}
        </div>
      </div>
    </>
  )
}

export default App
