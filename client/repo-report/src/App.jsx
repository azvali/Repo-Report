import './App.css'
import { useState } from 'react'
import ReactMarkdown from 'react-markdown'

function App() {
  const [overallSummary, setOverallSummary] = useState('');
  const [individualSummaries, setIndividualSummaries] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  
  const [num, setNum] = useState('');
  const [url, setUrl] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();

    // Client-side validation
    if(!num || !url){
      setError("Please fill out all fields.");
      return;
    }
    if(num > 30){
      setError("Please enter a number of 30 or less for the commits.");
      return;
    }

    setIsLoading(true);
    setError('');
    setOverallSummary('');
    setIndividualSummaries([]);

    try {
      const response = await fetch("http://localhost:5135/api/getSummaries", {
        method: "POST",
        headers : {"Content-Type" : "application/json"},
        body : JSON.stringify({
          num : parseInt(num),
          url : url
        })
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || 'The server returned an error.');
      }

      const data = await response.json();
      setOverallSummary(data.overallSummary);
      setIndividualSummaries(data.individualSummaries);

    } catch (err) {
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  };


  return (
    <>
      <div className='header'>
        <h1>Repo Report</h1>
      </div>
      <form className='input-field' onSubmit={handleSubmit}>
        <div>
          <input type='number' placeholder='#' value={num} onChange={(e) => {setNum(e.target.value)}} disabled={isLoading}></input>
          <input type='text' placeholder='Github URL' value={url} onChange={(e) => {setUrl(e.target.value)}} disabled={isLoading}></input>
        </div>
        <button type='submit' disabled={isLoading}>
          {isLoading ? 'Summarizing...' : 'Summarize'}
        </button>
      </form>

      {error && <div className='error-message'>{error}</div>}

      <div className='output'>
        {isLoading && <div className='loading-indicator'>Loading...</div>}

        {!isLoading && !error && overallSummary && (
          <>
            <div className='overall-summary'>
              <h2>Overall Summary</h2>
              <ReactMarkdown>{overallSummary}</ReactMarkdown>
            </div>
            <div className='single-commit'>
              <h2>Commit Details</h2>
              {individualSummaries.map((commit) => (
                <div key={commit.hash} className='commit-card'>
                  <h3>{commit.comment.split('\n')[0]}</h3>
                  <p>{commit.summary}</p>
                  <div className='commit-info'>
                    <span><strong>Committer:</strong> {commit.committer}</span>
                    <span><strong>Date:</strong> {new Date(commit.date).toLocaleString()}</span>
                    <a href={commit.commitURL} target="_blank" rel="noopener noreferrer">View on GitHub</a>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}
      </div>
    </>
  )
}

export default App
