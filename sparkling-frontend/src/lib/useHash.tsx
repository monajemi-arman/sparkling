'use client';

import { useState, useEffect } from 'react';

const useHash = () => {
    const [hash, setHash] = useState('');

    useEffect(() => {
        if (typeof window !== 'undefined') {
            setHash(window.location.hash);

            const handleHashChange = () => {
                setHash(window.location.hash);
            };

            window.addEventListener('hashchange', handleHashChange);

            return () => {
                window.removeEventListener('hashchange', handleHashChange);
            };
        }
    }, []);

    return hash;
};

export default useHash;