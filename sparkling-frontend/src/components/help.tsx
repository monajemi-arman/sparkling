import React, { useState, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import styles from './help.module.css';

const HelpPage: React.FC = () => {
  const [markdownContent, setMarkdownContent] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchHelpContent = async () => {
      try {
        const response = await fetch('/help.md');
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const text = await response.text();
        setMarkdownContent(text);
      } catch (e: any) {
        setError(`Failed to load help content: ${e.message}`);
        console.error("Error fetching help.md:", e);
      } finally {
        setLoading(false);
      }
    };

    fetchHelpContent();
  }, []);

  if (loading) {
    return (
      <div className={styles.helpPageContainer}>
        <p>Loading help content...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.helpPageContainer}>
        <p style={{ color: 'red' }}>{error}</p>
      </div>

    );
  }

  return (
    <div className={styles.helpPageContainer}>
      <ReactMarkdown remarkPlugins={[remarkGfm]}>
        {markdownContent}
      </ReactMarkdown>
    </div>
  );
};

export default HelpPage;
